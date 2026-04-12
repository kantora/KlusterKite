import React from 'react';
import { storiesOf, action } from '@kadira/storybook';
// import StubContainer from 'react-storybooks-relay-container';

import NodeTemplateForm from './NodeTemplateForm';

storiesOf('Node Templates')
  .add('edit', () => {
    const template = getTemplate().data.api.template;
    const packages = getPackages().data.api.klusterKiteNodesApi.nugetPackages;
    return <NodeTemplateForm onSubmit={action('submitted')} initialValues={template} packagesList={packages} />;
  })
  .add('edit saving', () => {
    const template = getTemplate().data.api.template;
    const packages = getPackages().data.api.klusterKiteNodesApi.nugetPackages;
    return <NodeTemplateForm onSubmit={action('submitted')} initialValues={template} packagesList={packages} saving={true} />;
  })
  .add('edit saved', () => {
    const template = getTemplate().data.api.template;
    const packages = getPackages().data.api.klusterKiteNodesApi.nugetPackages;
    return <NodeTemplateForm onSubmit={action('submitted')} initialValues={template} packagesList={packages} saved={true} />;
  })
  .add('edit save error', () => {
    const template = getTemplate().data.api.template;
    const packages = getPackages().data.api.klusterKiteNodesApi.nugetPackages;
    return <NodeTemplateForm onSubmit={action('submitted')} initialValues={template} packagesList={packages} saveError="Cannot update this record!" />;
  })
  .add('create', () => {
    const packages = getPackages().data.api.klusterKiteNodesApi.nugetPackages;
    return <NodeTemplateForm onSubmit={action('submitted')} packagesList={packages} />;
  })
;

const getTemplate = () => {
  return {
    "data": {
      "api": {
        "id": "W10=",
        "__typename": "KlusterKiteMonitoring_KlusterKiteNodeApi",
        "template": {
          "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJwdWJsaXNoZXIifV0=",
          "__typename": "KlusterKiteNodeApi_Template",
          "code": "publisher",
          "configuration": "{\n  KlusterKite {\n    Web {\n      Nginx {\n        PathToConfig = \"/etc/nginx/sites-enabled/klusterkite.config\"\n        ReloadCommand {\n          Command = /etc/init.d/nginx\n          Arguments = reload\n        } \n        Configuration {\n          default {\n            \"location /klusterkite\" { \n              proxy_pass = \"http://monitoringUI/klusterkite\"\n            }\n          }\n        }\n      }\n    }\n  }\n\n  akka {\n    remote {\n      helios {\n        tcp {\n          hostname = 0.0.0.0\n          port = 0\n        }\n      }\n    }\n    cluster {\n      seed-nodes = []\n    }\n  }\n}\n",
          "containerTypes": ["publisher"],
          "maximumNeededInstances": 1,
          "minimumRequiredInstances": 1,
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
              }, "cursor": "KlusterKite.Web.NginxConfigurator", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
            }, {
              "node": {
                "_id": "KlusterKite.NodeManager.Client",
                "specificVersion": null,
                "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJwdWJsaXNoZXIifSx7ImYiOiJwYWNrYWdlUmVxdWlyZW1lbnRzIiwiaWQiOiJDbHVzdGVyS2l0Lk5vZGVNYW5hZ2VyLkNsaWVudCJ9XQ==",
                "__typename": "KlusterKiteNodeApi_PackageRequirement"
              }, "cursor": "KlusterKite.NodeManager.Client", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
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
              }, "cursor": "KlusterKite.Log.ElasticSearch", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
            }, {
              "node": {
                "_id": "KlusterKite.Monitoring.Client",
                "specificVersion": null,
                "id": "W3siZiI6ImNsdXN0ZXJLaXROb2Rlc0FwaSJ9LHsiZiI6InJlbGVhc2VzIiwiaWQiOjF9LHsiZiI6ImNvbmZpZ3VyYXRpb24ifSx7ImYiOiJub2RlVGVtcGxhdGVzIiwiaWQiOiJwdWJsaXNoZXIifSx7ImYiOiJwYWNrYWdlUmVxdWlyZW1lbnRzIiwiaWQiOiJDbHVzdGVyS2l0Lk1vbml0b3JpbmcuQ2xpZW50In1d",
                "__typename": "KlusterKiteNodeApi_PackageRequirement"
              }, "cursor": "KlusterKite.Monitoring.Client", "__typename": "KlusterKiteNodeApi_PackageRequirement_Edge"
            }], "__typename": "KlusterKiteNodeApi_PackageRequirement_Connection"
          },
          "priority": 1000.0
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
                  "name": "KlusterKite.Core",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.Core.Service",
                  "version": "0.0.0-local",
                  "availableVersions": [
                    "0.0.0-local"
                  ]
                }
              },
              {
                "node": {
                  "name": "KlusterKite.Data.CRUD",
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
