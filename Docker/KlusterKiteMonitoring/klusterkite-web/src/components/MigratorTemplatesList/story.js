import React from 'react';
import { storiesOf } from '@kadira/storybook';
import StubContainer from 'react-storybooks-relay-container';

import MigratorTemplatesList from './MigratorTemplatesList';

storiesOf('Migrator Templates')
  .add('list, full access', () => {
    const node = getTemplatesListRelay();
    const props = {
      configuration: node.configuration,
      canEdit: true,
    };
    console.log(props);
    return <StubContainer Component={MigratorTemplatesList} props={props} />;
  })
  .add('list, no access to edit', () => {
    const node = getTemplatesListRelay();
    const props = {
      configuration: node.configuration,
      canEdit: false,
    };
    console.log(props);
    return <StubContainer Component={MigratorTemplatesList} props={props} />;
  })
;

const getTemplatesListRelay = () => {
  return {
    "configuration": {
      "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifV0=",
      "migratorTemplates": {
        "edges": [{
          "node": {
            "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJtaWdyYXRvclRlbXBsYXRlcyIsImlkIjoiQ2x1c3RlcktpdCJ9XQ==",
            "code": "KlusterKite",
            "configuration": "{\n\tKlusterKite.NodeManager.ConfigurationDatabaseConnectionString = \"User ID=postgres;Host=configDb;Port=5432;Pooling=true;Database=KlusterKite.NodeManagerConfiguration\"\t  \n    KlusterKite.NodeManager.Migrators = [\n        \"KlusterKite.NodeManager.ConfigurationSource.Migrator.ConfigurationMigrator, KlusterKite.NodeManager.ConfigurationSource.Migrator\"\n    ]\n}\n",
            "name": "KlusterKite Migrator",
            "notes": null,
            "packageRequirements": {
              "edges": [{
                "node": {
                  "_id": "KlusterKite.NodeManager.ConfigurationSource.Migrator",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJtaWdyYXRvclRlbXBsYXRlcyIsImlkIjoiQ2x1c3RlcktpdCJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTm9kZU1hbmFnZXIuQ29uZmlndXJhdGlvblNvdXJjZS5NaWdyYXRvciJ9XQ==",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.NodeManager.ConfigurationSource.Migrator",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.NodeManager",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJtaWdyYXRvclRlbXBsYXRlcyIsImlkIjoiQ2x1c3RlcktpdCJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTm9kZU1hbmFnZXIifV0=",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                }, "cursor": "KlusterKite.NodeManager", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Data.EF.Npgsql",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJtaWdyYXRvclRlbXBsYXRlcyIsImlkIjoiQ2x1c3RlcktpdCJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuRGF0YS5FRi5OcGdzcWwifV0=",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Data.EF.Npgsql",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }], "__typename": "KlusterKiteNodeApi_PackageRequirement_Connection"
            },
            "priority": 1.0,
            "__typename": "KlusterKiteNodeApi_MigratorTemplate"
          }, "cursor": "KlusterKite", "__typename": "KlusterKiteNodeApi_MigratorTemplate_Edge"
        }], "__typename": "KlusterKiteNodeApi_MigratorTemplate_Connection"
      },
      "nodeTemplates": {
        "edges": [{
          "node": {
            "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJwdWJsaXNoZXIifV0=",
            "code": "publisher",
            "configuration": "{\n  KlusterKite {\n    Web {\n      Nginx {\n        PathToConfig = \"/etc/nginx/sites-enabled/klusterkite.config\"\n        ReloadCommand {\n          Command = /etc/init.d/nginx\n          Arguments = reload\n        } \n        Configuration {\n          default {\n            \"location /klusterkite\" { \n              proxy_pass = \"http://monitoringUI/klusterkite\"\n            }\n          }\n        }\n      }\n    }\n  }\n\n  akka {\n    remote {\n      helios {\n        tcp {\n          hostname = 0.0.0.0\n          port = 0\n        }\n      }\n    }\n    cluster {\n      seed-nodes = []\n    }\n  }\n}\n",
            "containerTypes": ["publisher"],
            "minimumRequiredInstances": 1,
            "maximumNeededInstances": null,
            "name": "Cluster Nginx configurator",
            "packageRequirements": {
              "edges": [{
                "node": {
                  "_id": "KlusterKite.Core.Service",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJwdWJsaXNoZXIifSx7ImYiOiJwYWNrYWdlUmVxdWlyZW1lbnRzIiwiaWQiOiJDbHVzdGVyS2l0LkNvcmUuU2VydmljZSJ9XQ==",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                }, "cursor": "KlusterKite.Core.Service", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Web.NginxConfigurator",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJwdWJsaXNoZXIifSx7ImYiOiJwYWNrYWdlUmVxdWlyZW1lbnRzIiwiaWQiOiJDbHVzdGVyS2l0LldlYi5OZ2lueENvbmZpZ3VyYXRvciJ9XQ==",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Web.NginxConfigurator",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.NodeManager.Client",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJwdWJsaXNoZXIifSx7ImYiOiJwYWNrYWdlUmVxdWlyZW1lbnRzIiwiaWQiOiJDbHVzdGVyS2l0Lk5vZGVNYW5hZ2VyLkNsaWVudCJ9XQ==",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.NodeManager.Client",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Log.Console",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJwdWJsaXNoZXIifSx7ImYiOiJwYWNrYWdlUmVxdWlyZW1lbnRzIiwiaWQiOiJDbHVzdGVyS2l0LkxvZy5Db25zb2xlIn1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                }, "cursor": "KlusterKite.Log.Console", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Log.ElasticSearch",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJwdWJsaXNoZXIifSx7ImYiOiJwYWNrYWdlUmVxdWlyZW1lbnRzIiwiaWQiOiJDbHVzdGVyS2l0LkxvZy5FbGFzdGljU2VhcmNoIn1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Log.ElasticSearch",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Monitoring.Client",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJwdWJsaXNoZXIifSx7ImYiOiJwYWNrYWdlUmVxdWlyZW1lbnRzIiwiaWQiOiJDbHVzdGVyS2l0Lk1vbml0b3JpbmcuQ2xpZW50In1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Monitoring.Client",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }], "__typename": "KlusterKiteNodeApi_PackageRequirement_Connection"
            },
            "priority": 1000.0,
            "__typename": "KlusterKiteNodeApi_NodeTemplate"
          }, "cursor": "publisher", "__typename": "KlusterKiteNodeApi_NodeTemplate_Edge"
        }, {
          "node": {
            "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9XQ==",
            "code": "clusterManager",
            "configuration": "{\n  KlusterKite {\n\n    NodeManager.ConfigurationDatabaseConnectionString = \"User ID=postgres;Host=configDb;Port=5432;Pooling=true\"\n      \n    Web {\n\n      Swagger.Publish {\n          publishDocPath = \"\"klusterkite/manager/swagger/doc\"\"\n          publishUiPath = \"\"klusterkite/manager/swagger/ui\"\"\n      }\n\n      Services {\n        KlusterKite/Web/Swagger { // ServiceName is just unique service identification, used in order to handle stacked config properly. It is used just localy on node\n          Port = 8080 // default owin port, current node listening port for server access\n          PublicHostName = default //public host name of this service. It doesn't supposed (but is not prohibited) that this should be real public service hostname. It's just used to distinguish services with identical url paths to be correctly published on frontend web servers. Real expected hostname should be configured in NginxConfigurator or similar publisher\n          Route = /klusterkite/manager/swagger //route (aka directory) path to service\n        }                    \n      }\n\n    }\n  }\n\n\n\n  akka {\n    remote {\n      helios {\n        tcp {\n          hostname = 0.0.0.0\n          port = 0\n        }\n      }\n    }\n    cluster {\n      seed-nodes = []\n    }\n  }\n}\n",
            "containerTypes": ["manager", "worker"],
            "minimumRequiredInstances": 1,
            "maximumNeededInstances": 3,
            "name": "Cluster manager (cluster monitoring and managing)",
            "packageRequirements": {
              "edges": [{
                "node": {
                  "_id": "KlusterKite.Core.Service",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuQ29yZS5TZXJ2aWNlIn1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                }, "cursor": "KlusterKite.Core.Service", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.NodeManager.Client",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTm9kZU1hbmFnZXIuQ2xpZW50In1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.NodeManager.Client",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Monitoring.Client",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTW9uaXRvcmluZy5DbGllbnQifV0=",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Monitoring.Client",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Monitoring",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTW9uaXRvcmluZyJ9XQ==",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                }, "cursor": "KlusterKite.Monitoring", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.NodeManager",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTm9kZU1hbmFnZXIifV0=",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                }, "cursor": "KlusterKite.NodeManager", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Data.EF.Npgsql",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuRGF0YS5FRi5OcGdzcWwifV0=",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Data.EF.Npgsql",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Web.Swagger.Monitor",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuV2ViLlN3YWdnZXIuTW9uaXRvciJ9XQ==",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Web.Swagger.Monitor",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Web.Swagger",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuV2ViLlN3YWdnZXIifV0=",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                }, "cursor": "KlusterKite.Web.Swagger", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Log.Console",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTG9nLkNvbnNvbGUifV0=",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                }, "cursor": "KlusterKite.Log.Console", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Log.ElasticSearch",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTG9nLkVsYXN0aWNTZWFyY2gifV0=",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Log.ElasticSearch",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Web.Authentication",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuV2ViLkF1dGhlbnRpY2F0aW9uIn1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Web.Authentication",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Security.SessionRedis",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuU2VjdXJpdHkuU2Vzc2lvblJlZGlzIn1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Security.SessionRedis",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.API.Endpoint",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuQVBJLkVuZHBvaW50In1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                }, "cursor": "KlusterKite.API.Endpoint", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Web.GraphQL.Publisher",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJjbHVzdGVyTWFuYWdlciJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuV2ViLkdyYXBoUUwuUHVibGlzaGVyIn1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Web.GraphQL.Publisher",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }], "__typename": "KlusterKiteNodeApi_PackageRequirement_Connection"
            },
            "priority": 100.0,
            "__typename": "KlusterKiteNodeApi_NodeTemplate"
          }, "cursor": "clusterManager", "__typename": "KlusterKiteNodeApi_NodeTemplate_Edge"
        }, {
          "node": {
            "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJlbXB0eSJ9XQ==",
            "code": "empty",
            "configuration": "{\n  KlusterKite {\n\n    Web {\n\n      Services {\n        //KlusterKite/Web/Swagger { // ServiceName is just unique service identification, used in order to handle stacked config properly. It is used just localy on node\n          //Port = 8080 // default owin port, current node listening port for server access\n          //PublicHostName = default //public host name of this service. It doesn't supposed (but is not prohibited) that this should be real public service hostname. It's just used to distinguish services with identical url paths to be correctly published on frontend web servers. Real expected hostname should be configured in NginxConfigurator or similar publisher\n          //Route = /klusterkite/manager/swagger //route (aka directory) path to service\n        //}                    \n      }\n\n    }\n  }\n\n\n\n  akka {\n    remote {\n      helios {\n        tcp {\n          hostname = 0.0.0.0\n          port = 0\n        }\n      }\n    }\n    cluster {\n      seed-nodes = []\n    }\n  }\n}\n",
            "containerTypes": ["worker"],
            "minimumRequiredInstances": 0,
            "maximumNeededInstances": null,
            "name": "Cluster empty instance, just for demo",
            "packageRequirements": {
              "edges": [{
                "node": {
                  "_id": "KlusterKite.Core.Service",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJlbXB0eSJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuQ29yZS5TZXJ2aWNlIn1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                }, "cursor": "KlusterKite.Core.Service", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.NodeManager.Client",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJlbXB0eSJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTm9kZU1hbmFnZXIuQ2xpZW50In1d",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.NodeManager.Client",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }, {
                "node": {
                  "_id": "KlusterKite.Monitoring.Client",
                  "specificVersion": null,
                  "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJlbXB0eSJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTW9uaXRvcmluZy5DbGllbnQifV0=",
                  "__typename": "KlusterKiteNodeApi_PackageRequirement"
                },
                "cursor": "KlusterKite.Monitoring.Client",
                "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
              }], "__typename": "KlusterKiteNodeApi_PackageRequirement_Connection"
            },
            "priority": 1.0,
            "__typename": "KlusterKiteNodeApi_NodeTemplate"
          }, "cursor": "empty", "__typename": "KlusterKiteNodeApi_NodeTemplate_Edge"
        }], "__typename": "KlusterKiteNodeApi_NodeTemplate_Connection"
      }
    }
  };
};
