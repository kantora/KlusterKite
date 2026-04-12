import React from 'react';
import { storiesOf, action } from '@kadira/storybook';
// import StubContainer from 'react-storybooks-relay-container';

import MigratorTemplateForm from './MigratorTemplateForm';

storiesOf('Migrator Templates')
  .add('edit', () => {
    const template = getTemplate().data.api.template;
    const packages = getPackages().data.api.klusterKiteNodesApi.nugetPackages;
    return <MigratorTemplateForm onSubmit={action('submitted')} initialValues={template} packagesList={packages} />;
  })
  .add('edit saving', () => {
    const template = getTemplate().data.api.template;
    const packages = getPackages().data.api.klusterKiteNodesApi.nugetPackages;
    return <MigratorTemplateForm onSubmit={action('submitted')} initialValues={template} packagesList={packages} saving={true} />;
  })
  .add('edit saved', () => {
    const template = getTemplate().data.api.template;
    const packages = getPackages().data.api.klusterKiteNodesApi.nugetPackages;
    return <MigratorTemplateForm onSubmit={action('submitted')} initialValues={template} packagesList={packages} saved={true} />;
  })
  .add('edit save error', () => {
    const template = getTemplate().data.api.template;
    const packages = getPackages().data.api.klusterKiteNodesApi.nugetPackages;
    return <MigratorTemplateForm onSubmit={action('submitted')} initialValues={template} packagesList={packages} saveError="Cannot update this record!" />;
  })
  .add('create', () => {
    const packages = getPackages().data.api.klusterKiteNodesApi.nugetPackages;
    return <MigratorTemplateForm onSubmit={action('submitted')} packagesList={packages} />;
  })
;

const getTemplate = () => {
  return {
    "data": {
      "api": {
        "template": {
          "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjJ9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJtaWdyYXRvclRlbXBsYXRlcyIsImlkIjoiQ2x1c3RlcktpdCJ9XQ==",
          "__typename": "KlusterKiteNodeApi_MigratorTemplate",
          "code": "KlusterKite",
          "configuration": "{\n\tKlusterKite.NodeManager.ConfigurationDatabaseConnectionString = \"User ID=postgres;Host=configDb;Port=5432;Pooling=true;Database=KlusterKite.NodeManagerConfiguration\"\t  \n    KlusterKite.NodeManager.Migrators = [\n        \"KlusterKite.NodeManager.ConfigurationSource.Migrator.ConfigurationMigrator, KlusterKite.NodeManager.ConfigurationSource.Migrator\"\n    ]\n}\n",
          "name": "KlusterKite Migrator",
          "notes": null,
          "packageRequirements": {
            "edges": [{
              "node": {
                "_id": "KlusterKite.NodeManager.ConfigurationSource.Migrator",
                "specificVersion": null,
                "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjJ9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJtaWdyYXRvclRlbXBsYXRlcyIsImlkIjoiQ2x1c3RlcktpdCJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTm9kZU1hbmFnZXIuQ29uZmlndXJhdGlvblNvdXJjZS5NaWdyYXRvciJ9XQ==",
                "__typename": "KlusterKiteNodeApi_PackageRequirement"
              },
              "cursor": "KlusterKite.NodeManager.ConfigurationSource.Migrator",
              "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
            }, {
              "node": {
                "_id": "KlusterKite.NodeManager",
                "specificVersion": null,
                "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjJ9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJtaWdyYXRvclRlbXBsYXRlcyIsImlkIjoiQ2x1c3RlcktpdCJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuTm9kZU1hbmFnZXIifV0=",
                "__typename": "KlusterKiteNodeApi_PackageRequirement"
              }, "cursor": "KlusterKite.NodeManager", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
            }, {
              "node": {
                "_id": "KlusterKite.Data.EF.Npgsql",
                "specificVersion": null,
                "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjJ9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJtaWdyYXRvclRlbXBsYXRlcyIsImlkIjoiQ2x1c3RlcktpdCJ9LHsiZiI6InBhY2thZ2VSZXF1aXJlbWVudHMiLCJpZCI6IkNsdXN0ZXJLaXQuRGF0YS5FRi5OcGdzcWwifV0=",
                "__typename": "KlusterKiteNodeApi_PackageRequirement"
              }, "cursor": "KlusterKite.Data.EF.Npgsql", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
            }], "__typename": "KlusterKiteNodeApi_PackageRequirement_Connection"
          },
          "priority": 1.0
        }
      }
    }
  };
};

const getPackages = () => {
  return {
    "data": {
      "api": {
        "klusterKiteNodesApi": {
          "nugetPackages": {
            "edges": [
              {
                "node": {
                  "name": "KlusterKite.API.Client",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local",
                    "0.0.1",
                    "0.0.12",
                    "0.0.2",
                    "0.0.5 beta",
                    "0.1.0",
                    "0.1.1"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.NodeManager.ConfigurationSource.Migrator",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.NodeManager",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.Data.EF.Npgsql",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
              {
                "node": {
                  "name": "Microsoft.AspNet.Cors",
                  "version": "5.2.3",
                  "availableVersions": [
                    "5.2.3"
                  ]
                }
              },
              {
                "node": {
                  "name": "Microsoft.AspNet.Mvc",
                  "version": "5.2.3",
                  "availableVersions": [
                    "5.2.3"
                  ]
                }
              },
              {
                "node": {
                  "name": "Microsoft.AspNet.Razor",
                  "version": "3.2.3",
                  "availableVersions": [
                    "3.2.3"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.Web.NginxConfigurator",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.Web.NginxConfigurator",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.NodeManager.Client",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.Log.Console",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.Log.ElasticSearch",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.Monitoring.Client",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
            ]
          }
        }
      }
    }
  }
};
