import React from 'react';
import { storiesOf, action } from '@kadira/storybook';
import StubContainer from 'react-storybooks-relay-container';

import NodesWithTemplates from './NodesWithTemplates';

storiesOf('Homepage')
  .add('nodes with templates', () => {
    const data = getData();

    const props = {
      data: data.data.api.klusterKiteNodesApi,
      hasError: false,
      upgradeNodePrivilege: true,
      onManualUpgrade: action('onManualUpgrade')
    };
    return <StubContainer Component={NodesWithTemplates} props={props} />;
  })
;

const getData = () => {
  return {
    "data": {
      "api": {
        "__typename": "KlusterKiteMonitoring_KlusterKiteNodeApi",
        "klusterKiteNodesApi": {
          "id": "W3siZiI6Im5vZGVNYW5hZ2VyRGF0YSJ9XQ==",
          "getActiveNodeDescriptions": {
            "edges": [{
              "node": {
                "nodeTemplate": null,
                "id": "W3siZiI6Im5vZGVNYW5hZ2VyRGF0YSJ9LHsiZiI6ImdldEFjdGl2ZU5vZGVEZXNjcmlwdGlvbnMiLCJpZCI6ImY2ZThiNWE0LTBhNDItNDhhOS1hYWVjLTY0N2Q2Nzg5OWE3YSJ9XQ=="
              }, "cursor": "f6e8b5a4-0a42-48a9-aaec-647d67899a7a"
            }, {
              "node": {
                "nodeTemplate": "clusterManager",
                "id": "W3siZiI6Im5vZGVNYW5hZ2VyRGF0YSJ9LHsiZiI6ImdldEFjdGl2ZU5vZGVEZXNjcmlwdGlvbnMiLCJpZCI6ImEyY2ZmNjY1LTYxZjYtNDUyZC04ZmQwLThlZTgwYmM0MDMxNiJ9XQ=="
              }, "cursor": "a2cff665-61f6-452d-8fd0-8ee80bc40316"
            }, {
              "node": {
                "nodeTemplate": "clusterManager",
                "id": "W3siZiI6Im5vZGVNYW5hZ2VyRGF0YSJ9LHsiZiI6ImdldEFjdGl2ZU5vZGVEZXNjcmlwdGlvbnMiLCJpZCI6IjEwMDc1ODY5LTY3MDktNDE1MS04ZTdlLWZiZGEyNWZlYjE5MiJ9XQ=="
              }, "cursor": "10075869-6709-4151-8e7e-fbda25feb192"
            }, {
              "node": {
                "nodeTemplate": "publisher",
                "id": "W3siZiI6Im5vZGVNYW5hZ2VyRGF0YSJ9LHsiZiI6ImdldEFjdGl2ZU5vZGVEZXNjcmlwdGlvbnMiLCJpZCI6ImI0MGNiZjFjLTY5MzQtNDNjMS05MzczLWU3OWFlN2Q3YWEzNyJ9XQ=="
              }, "cursor": "b40cbf1c-6934-43c1-9373-e79ae7d7aa37"
            }, {
              "node": {
                "nodeTemplate": "publisher",
                "id": "W3siZiI6Im5vZGVNYW5hZ2VyRGF0YSJ9LHsiZiI6ImdldEFjdGl2ZU5vZGVEZXNjcmlwdGlvbnMiLCJpZCI6ImFiYWE1YzU3LWRlMTYtNDIxZC05NjQzLWE5ZDkzZWFjYmMwNSJ9XQ=="
              }, "cursor": "abaa5c57-de16-421d-9643-a9d93eacbc05"
            }]
          },
          "nodeTemplates": {
            "edges": [{
              "node": {
                "id": "W3siZiI6Im5vZGVNYW5hZ2VyRGF0YSJ9LHsiZiI6Im5vZGVUZW1wbGF0ZXMiLCJpZCI6MX1d",
                "code": "publisher",
                "minimumRequiredInstances": 1,
                "name": "Cluster Nginx configurator"
              }, "cursor": 1
            }, {
              "node": {
                "id": "W3siZiI6Im5vZGVNYW5hZ2VyRGF0YSJ9LHsiZiI6Im5vZGVUZW1wbGF0ZXMiLCJpZCI6Mn1d",
                "code": "clusterManager",
                "minimumRequiredInstances": 1,
                "name": "Cluster manager (cluster monitoring and managing)"
              }, "cursor": 2
            }, {
              "node": {
                "id": "W3siZiI6Im5vZGVNYW5hZ2VyRGF0YSJ9LHsiZiI6Im5vZGVUZW1wbGF0ZXMiLCJpZCI6M31d",
                "code": "empty",
                "minimumRequiredInstances": 0,
                "name": "Cluster empty instance, just for demo"
              }, "cursor": 3
            }]
          }
        },
        "id": "W10="
      }
    }
  };
};
