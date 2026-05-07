#!/usr/bin/env python3
"""
KlusterKite cluster upgrade driver.

Drives the full version-bump-and-migrate flow against a running cluster:

  1. Resolve the current Active configuration.
  2. Create a new Draft cloned from it, with a bumped version and the
     packages list refreshed from the live NuGet feed.
  3. Run the configuration check, set Ready, start migration.
  4. Walk the migration steps: per-template resource updates (Pre and
     Post node phases), node updates to Destination, and Finish.
  5. Verify the new configuration is Active.

Assumes packages with the new versions are already on the local NuGet
feed. Run `dotnet cake build.cake --target=FinalPushLocalPackages`
beforehand (or pass --skip-build=false; this script does NOT call cake
itself, to keep concerns separated).

Usage:
  python upgrade.py
  python upgrade.py --api http://localhost:28080 --bump minor
  python upgrade.py --notes "akka 1.5.67 -> 1.5.68; PackageUtils null fix"
  python upgrade.py --name "Release 0.4"   # override the default timestamp name
  python upgrade.py --dry-run        # plan, but stop before mutation
  python upgrade.py --abort-current  # cancel any in-flight migration first

Naming: by default the new Configuration is named `Release YYYYMMDDHHMMSS`
(UTC). Pass --name to override. The version (majorVersion.minorVersion)
fields are independently bumped via --bump.

Notes: the agent SHOULD pass --notes describing what's actually changing
(e.g. "Akka 1.5.67 -> 1.5.68", "fix EnumResolver null on enum",
"#issue-123: monitoring graphql provider crash on null state"). The
notes show up in the Configurations list in the UI and are the only
human-readable audit trail of why each migration happened. Empty notes
are accepted but make the deploy log useless after a few rounds.

Exit codes:
  0  upgrade complete, new config is Active
  1  invalid arguments / unrecoverable state
  2  blocked by cluster state (e.g. Check failed; resolution errors)
  3  migration failed and was rolled back
"""

import argparse
import datetime
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request


def _http(url, *, method="POST", headers=None, data=None, timeout=30):
    req = urllib.request.Request(url, method=method, data=data)
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.status, resp.read()
    except urllib.error.HTTPError as e:
        return e.code, e.read()


def get_token(api, user, password, client_id):
    body = urllib.parse.urlencode({
        "grant_type": "password",
        "username": user,
        "password": password,
        "client_id": client_id,
    }).encode()
    status, raw = _http(
        f"{api}/api/1.x/security/token",
        headers={"Content-Type": "application/x-www-form-urlencoded"},
        data=body,
    )
    if status != 200:
        raise SystemExit(f"auth failed ({status}): {raw[:200]!r}")
    return json.loads(raw)["access_token"]


def gql(api, token, query, *, expect_errors_in_payload=False):
    """Run a GraphQL request. Raises on transport errors. If
    `expect_errors_in_payload` is False, also raises when the response
    contains an `errors` array — set it True when the call's payload
    legitimately surfaces validation errors (e.g. configurations_check)."""
    status, raw = _http(
        f"{api}/api/1.x/graphQL",
        headers={
            "Content-Type": "application/json",
            "Authorization": f"Bearer {token}",
        },
        data=json.dumps({"query": query}).encode(),
        timeout=60,
    )
    if status != 200:
        raise SystemExit(f"graphql {status}: {raw[:400]!r}")
    body = json.loads(raw)
    if not expect_errors_in_payload and body.get("errors"):
        raise SystemExit(f"graphql errors: {json.dumps(body['errors'], indent=2)}")
    return body


def get_active_config_full(api, token):
    """Pull every field needed to clone the active config into a draft."""
    body = gql(api, token, """{
      api { klusterKiteNodesApi { configurations(filter:{state:Active},limit:1){
        edges{node{
          _id name majorVersion minorVersion notes
          settings{
            nugetFeed
            seedAddresses
            packages{ edges{ node{ _id version } } }
            nodeTemplates{ edges{ node{
              code name notes
              minimumRequiredInstances maximumNeededInstances
              priority containerTypes configuration
              packageRequirements{ edges{ node{ _id specificVersion } } }
            }}}
            migratorTemplates{ edges{ node{
              code name notes priority configuration
              packageRequirements{ edges{ node{ _id specificVersion } } }
            }}}
          }
        }}
      }}}
    }""")
    edges = body["data"]["api"]["klusterKiteNodesApi"]["configurations"]["edges"]
    if not edges:
        raise SystemExit("no Active configuration found")
    return edges[0]["node"]


def get_nuget_packages(api, token):
    """Latest available version per package id on the local nuget feed."""
    body = gql(api, token, """{
      api { klusterKiteNodesApi { nugetPackages{
        edges { node { name version } }
      }}}
    }""")
    return [
        {"id": e["node"]["name"], "version": e["node"]["version"]}
        for e in body["data"]["api"]["klusterKiteNodesApi"]["nugetPackages"]["edges"]
    ]


def settings_input_from_active(active_node, refreshed_packages):
    """Build a ConfigurationSettings_Input value cloning the active
    config's settings but with `packages` replaced by the latest
    versions queried from the live nuget feed."""
    s = active_node["settings"]

    def pkg_reqs(template):
        return [
            {"id": pr["node"]["_id"], "specificVersion": pr["node"]["specificVersion"]}
            for pr in template["packageRequirements"]["edges"]
        ]

    def node_template(t):
        return {
            "code": t["code"],
            "name": t["name"],
            "notes": t.get("notes"),
            "minimumRequiredInstances": t["minimumRequiredInstances"],
            "maximumNeededInstances": t["maximumNeededInstances"],
            "priority": t["priority"],
            "containerTypes": t["containerTypes"],
            "configuration": t["configuration"],
            "packageRequirements": pkg_reqs(t),
        }

    def migrator_template(t):
        return {
            "code": t["code"],
            "name": t["name"],
            "notes": t.get("notes"),
            "priority": t["priority"],
            "configuration": t["configuration"],
            "packageRequirements": pkg_reqs(t),
        }

    return {
        "nugetFeed": s["nugetFeed"],
        "seedAddresses": s["seedAddresses"],
        "packages": refreshed_packages,
        "nodeTemplates": [
            node_template(e["node"]) for e in s["nodeTemplates"]["edges"]
        ],
        "migratorTemplates": [
            migrator_template(e["node"]) for e in s["migratorTemplates"]["edges"]
        ],
    }


def _gql_lit(value):
    """Render a Python value as a GraphQL input literal (NOT a JSON string).
    Quotes strings, recurses into lists/dicts, lowercases booleans, leaves
    numbers and None as-is."""
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return str(value)
    if isinstance(value, str):
        return json.dumps(value)  # JSON string == GraphQL string literal here
    if isinstance(value, list):
        return "[" + ",".join(_gql_lit(v) for v in value) + "]"
    if isinstance(value, dict):
        return "{" + ",".join(f"{k}:{_gql_lit(v)}" for k, v in value.items()) + "}"
    raise TypeError(f"unsupported {type(value).__name__}: {value!r}")


def create_draft(api, token, *, major, minor, name, notes):
    body = gql(api, token, f"""mutation {{
      klusterKiteNodeApi_klusterKiteNodesApi_configurations_create(input:{{
        newNode:{{
          majorVersion:{major}, minorVersion:{minor},
          name:{json.dumps(name)}, notes:{json.dumps(notes or "")}
        }},
        clientMutationId:"upgrade"
      }}){{ node{{ _id }} errors{{ edges{{ node{{ field message }} }} }} }}
    }}""", expect_errors_in_payload=True)
    payload = body["data"]["klusterKiteNodeApi_klusterKiteNodesApi_configurations_create"]
    errs = payload.get("errors") or {}
    if errs and errs.get("edges"):
        raise SystemExit(f"create failed: {json.dumps(errs, indent=2)}")
    return payload["node"]["_id"]


def write_settings(api, token, config_id, settings_input, *, name, major, minor, notes):
    body = gql(api, token, f"""mutation {{
      klusterKiteNodeApi_klusterKiteNodesApi_configurations_update(input:{{
        id:{config_id},
        newNode:{{
          id:{config_id}, majorVersion:{major}, minorVersion:{minor},
          name:{json.dumps(name)}, notes:{json.dumps(notes or "")},
          settings:{_gql_lit(settings_input)}
        }},
        clientMutationId:"upgrade"
      }}){{ node{{ _id }} errors{{ edges{{ node{{ field message }} }} }} }}
    }}""", expect_errors_in_payload=True)
    payload = body["data"]["klusterKiteNodeApi_klusterKiteNodesApi_configurations_update"]
    errs = payload.get("errors") or {}
    if errs and errs.get("edges"):
        raise SystemExit(f"update failed: {json.dumps(errs, indent=2)}")


def configuration_check(api, token, config_id):
    body = gql(api, token, f"""mutation {{
      klusterKiteNodeApi_klusterKiteNodesApi_configurations_check(input:{{
        id:{config_id}, clientMutationId:"upgrade"
      }}){{ result errors{{ edges{{ node{{ field message }} }} }} }}
    }}""", expect_errors_in_payload=True)
    payload = body["data"]["klusterKiteNodeApi_klusterKiteNodesApi_configurations_check"]
    errs = payload.get("errors") or {}
    if errs and errs.get("edges"):
        raise SystemExit(2, f"check failed: {json.dumps(errs, indent=2)}")
    if payload["result"] is False:
        raise SystemExit(2, "check returned false")


def set_ready(api, token, config_id):
    body = gql(api, token, f"""mutation {{
      klusterKiteNodeApi_klusterKiteNodesApi_configurations_setReady(input:{{
        id:{config_id}, clientMutationId:"upgrade"
      }}){{ result errors{{ edges{{ node{{ field message }} }} }} }}
    }}""", expect_errors_in_payload=True)
    payload = body["data"]["klusterKiteNodeApi_klusterKiteNodesApi_configurations_setReady"]
    errs = payload.get("errors") or {}
    if errs and errs.get("edges"):
        raise SystemExit(f"setReady failed: {json.dumps(errs, indent=2)}")


def migration_create(api, token, config_id):
    body = gql(api, token, f"""mutation {{
      klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationCreate(input:{{
        newConfigurationId:{config_id}, clientMutationId:"upgrade"
      }}){{ result errors{{ edges{{ node{{ field message }} }} }} }}
    }}""", expect_errors_in_payload=True)
    payload = body["data"]["klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationCreate"]
    errs = payload.get("errors") or {}
    if errs and errs.get("edges"):
        raise SystemExit(f"migrationCreate failed: {json.dumps(errs, indent=2)}")


def migration_cancel(api, token):
    gql(api, token, """mutation {
      klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationCancel(input:{
        clientMutationId:"upgrade"
      }) { result }
    }""")


def migration_finish(api, token):
    gql(api, token, """mutation {
      klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationFinish(input:{
        clientMutationId:"upgrade"
      }) { result }
    }""")


def migration_nodes_update(api, token, target):
    """target ∈ {Source, Destination}"""
    gql(api, token, f"""mutation {{
      klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationNodesUpdate(input:{{
        target:{target}, clientMutationId:"upgrade"
      }}) {{ result }}
    }}""")


def migration_resource_update(api, token, resources):
    """resources: list of {templateCode, migratorTypeName, resourceCode, target}"""
    gql(api, token, f"""mutation {{
      klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationResourceUpdate(input:{{
        request:{{ resources:{_gql_lit(resources)} }},
        clientMutationId:"upgrade"
      }}) {{ result }}
    }}""")


def upgrade_node(api, token, address):
    """Manually request a node to restart and reload its configuration.

    Equivalent to the per-node Reset button on the home page. Required for
    NodeTemplates whose minimumRequiredInstances == maximumNeededInstances
    (or any pin that prevents the migration's automatic node turnover):
    the cluster won't take such a node down on its own — it can't preserve
    capacity while the replacement comes up. So those nodes stay
    isObsolete=true after migrationNodesUpdate completes, and
    canFinishMigration won't go true until they're cycled. Send
    upgradeNode(address) to each obsolete node and wait for it to drop
    out of getActiveNodeDescriptions and rejoin under the new config.
    """
    gql(api, token, f"""mutation {{
      klusterKiteNodeApi_klusterKiteNodesApi_upgradeNode(input:{{
        address:{json.dumps(address)}, clientMutationId:"upgrade"
      }}) {{ result {{ result }} }}
    }}""")


def get_active_nodes(api, token):
    body = gql(api, token, """{
      api { klusterKiteNodesApi { getActiveNodeDescriptions { edges { node {
        nodeId nodeTemplate isObsolete isInitialized
        nodeAddress { asString }
      }}}}}
    }""")
    return [
        e["node"]
        for e in body["data"]["api"]["klusterKiteNodesApi"]["getActiveNodeDescriptions"]["edges"]
    ]


def cycle_obsolete_stragglers(api, token, *, timeout, interval=10):
    """Find every obsolete node and ask it to restart. Wait for them all
    to drop their obsolete flag (or be replaced by a new instance under
    the same template). Returns the count of nodes cycled."""
    obsolete = [n for n in get_active_nodes(api, token) if n.get("isObsolete")]
    if not obsolete:
        return 0
    print(f"  found {len(obsolete)} obsolete node(s) the migration won't auto-cycle "
          "(min-instance pin); sending manual restart:", flush=True)
    addresses_seen = set()
    for n in obsolete:
        addr = n["nodeAddress"]["asString"]
        tpl = n.get("nodeTemplate") or "?"
        print(f"    upgradeNode({addr})  [template={tpl}]", flush=True)
        upgrade_node(api, token, addr)
        addresses_seen.add(addr)

    start = time.time()
    while True:
        nodes = get_active_nodes(api, token)
        # The address the launcher reports back changes when the node
        # rejoins (new ephemeral port). So "settled" means: every address
        # we just kicked is gone AND no obsolete nodes remain.
        still_obsolete = [n for n in nodes if n.get("isObsolete")]
        still_old_addrs = [n for n in nodes if n["nodeAddress"]["asString"] in addresses_seen]
        if not still_obsolete and not still_old_addrs:
            elapsed = int(time.time() - start)
            print(f"  obsolete stragglers cycled in {elapsed}s", flush=True)
            return len(addresses_seen)
        if time.time() - start > timeout:
            raise SystemExit(
                f"timeout {timeout}s waiting for {len(addresses_seen)} restarted node(s) "
                f"to rejoin (still obsolete: {len(still_obsolete)}, "
                f"still on old address: {len(still_old_addrs)})"
            )
        time.sleep(interval)


def get_migration_state(api, token):
    body = gql(api, token, """{
      api { klusterKiteNodesApi { clusterManagement {
        currentMigration { state fromConfiguration{_id name} toConfiguration{_id name} }
        resourceState {
          currentMigrationStep
          canCancelMigration canFinishMigration canMigrateResources
          canUpdateNodesToDestination canUpdateNodesToSource
          operationIsInProgress
          migrationState{ templateStates{ edges{ node{
            code
            migrators{ edges{ node{
              typeName direction
              resources{ edges{ node{ code position migrationToDestinationExecutor migrationToSourceExecutor key } } }
            }}}
          }}}}
          migratableResources: migrationState{ migratableResources{ edges{ node{ key } } } }
        }
      }}}
    }""")
    return body["data"]["api"]["klusterKiteNodesApi"]["clusterManagement"]


def collect_resources_to_destination(state):
    """From the migration state, return ResourceUpgrade_Input items for
    every resource currently NOT at Destination but with a forward
    executor available (i.e., migratable forward)."""
    rs = state.get("resourceState") or {}
    ms = rs.get("migrationState") or {}
    ts = (ms.get("templateStates") or {}).get("edges") or []
    migratable_keys = {
        e["node"]["key"]
        for e in ((rs.get("migratableResources") or {}).get("migratableResources") or {}).get("edges", [])
    }
    out = []
    for t in ts:
        tnode = t["node"]
        for m in tnode.get("migrators", {}).get("edges", []):
            mnode = m["node"]
            for r in mnode.get("resources", {}).get("edges", []):
                rn = r["node"]
                if rn["position"] == "Destination":
                    continue
                if rn["migrationToDestinationExecutor"] is None:
                    continue
                if rn["key"] not in migratable_keys:
                    continue
                out.append({
                    "templateCode": tnode["code"],
                    "migratorTypeName": mnode["typeName"],
                    "resourceCode": rn["code"],
                    "target": "Destination",
                })
    return out


def wait_until(api, token, predicate, *, timeout=600, interval=5, label=""):
    """Poll get_migration_state until predicate(state) returns True or
    timeout elapses. Returns the final state."""
    start = time.time()
    last_step = object()
    while True:
        state = get_migration_state(api, token)
        rs = state.get("resourceState") or {}
        cm = state.get("currentMigration")
        step = (rs or {}).get("currentMigrationStep")
        op = (rs or {}).get("operationIsInProgress")
        m_state = (cm or {}).get("state") if cm else None
        marker = (m_state, step, op)
        if marker != last_step:
            print(f"  [{int(time.time()-start):>4}s] migration={m_state} step={step} op_in_progress={op}", flush=True)
            last_step = marker
        if predicate(state):
            return state
        if time.time() - start > timeout:
            raise SystemExit(f"timeout {timeout}s waiting for: {label}")
        time.sleep(interval)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--api", default=os.environ.get("KK_API", "http://localhost:28080"))
    p.add_argument("--user", default=os.environ.get("KK_USER", "admin"))
    p.add_argument("--password", default=os.environ.get("KK_PASSWORD", "admin"))
    p.add_argument("--client-id", default=os.environ.get("KK_CLIENT_ID", "KlusterKite.NodeManager.WebApplication"))
    p.add_argument("--bump", choices=["patch", "minor", "major"], default="minor",
                   help="how to bump the version on the new configuration (default: minor)")
    p.add_argument("--name", help="name for the new Configuration; default: 'Release YYYYMMDDHHMMSS' (UTC)")
    p.add_argument("--notes", default="",
                   help="free-text notes describing what's changing in this release. "
                        "Surface in the Configurations list and is the only human-readable "
                        "audit trail of why each migration happened — supply something useful "
                        "(e.g., 'Akka 1.5.67 -> 1.5.68; PackageUtils null fix; #issue-42').")
    p.add_argument("--dry-run", action="store_true",
                   help="plan + print the new settings, but make no mutations")
    p.add_argument("--abort-current", action="store_true",
                   help="if a migration is currently in flight, cancel it before starting")
    p.add_argument("--no-finish", action="store_true",
                   help="walk migration up to canFinishMigration but don't call Finish; user does it manually")
    p.add_argument("--timeout", type=int, default=900,
                   help="per-step timeout in seconds (default: 900)")
    args = p.parse_args()

    api = args.api.rstrip("/")
    print(f"=== KlusterKite cluster upgrade against {api} ===", flush=True)

    token = get_token(api, args.user, args.password, args.client_id)
    print("auth ok", flush=True)

    cm = get_migration_state(api, token).get("currentMigration")
    if cm:
        print(f"  in-flight migration detected: {cm['fromConfiguration']['name']} → {cm['toConfiguration']['name']} ({cm['state']})", flush=True)
        if args.abort_current:
            migration_cancel(api, token)
            print("  cancelled", flush=True)
            wait_until(api, token, lambda s: s.get("currentMigration") is None,
                       timeout=120, interval=3, label="migration to clear")
        else:
            raise SystemExit("a migration is already in flight; pass --abort-current to cancel and continue")

    active = get_active_config_full(api, token)
    print(f"active: #{active['_id']} '{active['name']}' v{active['majorVersion']}.{active['minorVersion']}", flush=True)

    major, minor = active["majorVersion"], active["minorVersion"]
    if args.bump == "patch":
        # Configuration model has no patch field — bump is treated as minor.
        new_major, new_minor = major, minor + 1
    elif args.bump == "minor":
        new_major, new_minor = major, minor + 1
    elif args.bump == "major":
        new_major, new_minor = major + 1, 0
    name = args.name or "Release " + datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%d%H%M%S")
    if not args.notes:
        print("note: --notes was empty. Once this Configuration is Active there's no other "
              "place to look up what changed. Consider re-running with --notes describing "
              "the package version diff or the issue number.", flush=True)

    refreshed = get_nuget_packages(api, token)
    print(f"latest packages on feed: {len(refreshed)}", flush=True)
    settings_input = settings_input_from_active(active, refreshed)

    if args.dry_run:
        print(json.dumps({"target_name": name, "major": new_major, "minor": new_minor,
                          "settings_preview": {**settings_input, "packages": settings_input["packages"][:5] + ["...truncated"]}}, indent=2))
        return 0

    new_id = create_draft(api, token, major=new_major, minor=new_minor, name=name, notes=args.notes)
    print(f"created draft #{new_id}", flush=True)

    write_settings(api, token, new_id, settings_input,
                   name=name, major=new_major, minor=new_minor, notes=args.notes)
    print("settings written", flush=True)

    configuration_check(api, token, new_id)
    print("check ok", flush=True)

    set_ready(api, token, new_id)
    print(f"#{new_id} → Ready", flush=True)

    migration_create(api, token, new_id)
    print(f"migration started: → #{new_id}", flush=True)

    # Walk the migration. The shape: Preparing → (Pre resources) → NodesUpdating
    # → (Post resources) → Finish. After each step the cluster sets
    # canFinishMigration once everything is at Destination.
    print("waiting for migration to leave Preparing…", flush=True)
    wait_until(api, token,
               lambda s: ((s.get("resourceState") or {}).get("currentMigrationStep") not in (None, "Start")),
               timeout=args.timeout, interval=5, label="leave Preparing")

    while True:
        state = get_migration_state(api, token)
        rs = state.get("resourceState") or {}
        step = rs.get("currentMigrationStep")
        if step in ("ResourcesUpdated", "PreNodesResourcesUpdating", "PreNodeResourcesUpdated", "Finish"):
            # Try to migrate any remaining forward-migratable resources.
            resources = collect_resources_to_destination(state)
            if resources:
                print(f"  step={step}: migrating {len(resources)} resources to Destination", flush=True)
                migration_resource_update(api, token, resources)
                wait_until(api, token,
                           lambda s: not (s.get("resourceState") or {}).get("operationIsInProgress"),
                           timeout=args.timeout, interval=5, label="resource migration to settle")
                continue

        if rs.get("canUpdateNodesToDestination"):
            print(f"  step={step}: updating nodes to Destination", flush=True)
            migration_nodes_update(api, token, "Destination")
            wait_until(api, token,
                       lambda s: ((s.get("resourceState") or {}).get("currentMigrationStep") != "NodesUpdating"
                                   and not (s.get("resourceState") or {}).get("operationIsInProgress")),
                       timeout=args.timeout, interval=10, label="nodes update to settle")
            # Templates pinned to a fixed instance count (minimumRequiredInstances ==
            # maximumNeededInstances) are not cycled automatically because the
            # cluster won't drop below the minimum. Cycle them by hand.
            cycle_obsolete_stragglers(api, token, timeout=args.timeout)
            continue

        # Even outside of NodesUpdating, an obsolete node might appear if a
        # template was edited mid-migration. Sweep before deciding to Finish.
        if cycle_obsolete_stragglers(api, token, timeout=args.timeout):
            continue

        if rs.get("canFinishMigration"):
            if args.no_finish:
                print("canFinishMigration=true; --no-finish set, exiting (manual finish required)")
                return 0
            migration_finish(api, token)
            print("migrationFinish issued; waiting for the cluster to clear the migration record…", flush=True)
            wait_until(api, token,
                       lambda s: s.get("currentMigration") is None,
                       timeout=args.timeout, interval=5, label="currentMigration to clear")
            # Final post-finish sweep: a node template may still have an
            # obsolete instance the cluster won't auto-cycle.
            cycle_obsolete_stragglers(api, token, timeout=args.timeout)
            break

        if state.get("currentMigration") is None:
            # Migration disappeared without us calling Finish: either it
            # rolled back (Failed) or another actor cancelled it.
            print("currentMigration is null; verifying Active configuration", flush=True)
            break

        # Nothing actionable yet. Wait and re-check.
        wait_until(api, token, lambda _: False,
                   timeout=15, interval=5, label="(idle)") if False else time.sleep(5)

    new_active = get_active_config_full(api, token)
    if new_active["_id"] == new_id:
        print(f"OK: #{new_id} '{new_active['name']}' is now Active", flush=True)
        return 0
    print(f"WARNING: expected #{new_id} to be Active, but Active is #{new_active['_id']} ('{new_active['name']}')", flush=True)
    return 3


if __name__ == "__main__":
    sys.exit(main())
