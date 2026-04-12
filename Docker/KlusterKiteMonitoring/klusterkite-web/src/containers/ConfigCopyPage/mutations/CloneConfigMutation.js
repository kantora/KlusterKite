import Relay from 'react-relay'

export default class CloneConfigMutation extends Relay.Mutation {
  getMutation () {
    return Relay.QL`mutation{klusterKiteNodeApi_klusterKiteNodesApi_configurations_update}`
  }

  getFatQuery () {
    return Relay.QL`
      fragment on KlusterKiteNodeApi_Configuration_NodeMutationPayload {
        node
        edge
        errors {
          edges {
            node {
              field
              message
            }
          }
        }
        api {
          klusterKiteNodesApi {
            configurations
          }
        }
      }
    `
  }

  getConfigs () {
    return [{
      type: 'REQUIRED_CHILDREN',
      children: [
        Relay.QL`
          fragment on KlusterKiteNodeApi_Configuration_NodeMutationPayload {
            errors {
              edges {
                node {
                  field
                  message
                }
              }
            }
            node
          }
        `,
      ],
    }]
  }

  /**
   * Convert edges list to an array of nodes; cleans unnecessary properties
   * @param edges {Object} Edges list
   * @param type {string} Converted object type
   * @returns {Object[]} Array of nodes
   */
  convertEdgesToArray(edges, type){
    const oldNodes = edges.map(x => x.node);

    let nodes = [];
    oldNodes.forEach(node => {
      const keys = Object.keys(node);
      let newNode = {};
      keys.forEach(key => {
        if (key !== '_id' && key !== '__dataID__' && key !== 'id' && key !== 'packagesToInstall'){
          if (typeof(node[key]) === 'object' && node[key] && node[key].edges) {
            newNode[key] = this.convertEdgesToArray(node[key].edges, key);
          } else {
            newNode[key] = node[key];
          }
        }

        if (type === 'packages' && key === '_id') {
          newNode['id'] = node[key];
        }

        if (type === 'packageRequirements' && key === '_id') {
          newNode['id'] = node[key];
        }
      });

      if (newNode) {
        nodes.push(newNode);
      }
    });

    if (type === 'migrationTemplates') {
      console.log('migrator templates', nodes);
    }

    return nodes;
  }

  getVariables () {
    return {
      id: this.props.configurationId,
      newNode: {
        id: this.props.configurationId,
        settings: {
          migratorTemplates: this.convertEdgesToArray(this.props.settings.migratorTemplates.edges, 'migrationTemplates'),
          nodeTemplates: this.convertEdgesToArray(this.props.settings.nodeTemplates.edges, 'nodeTemplates'),
          nugetFeed: this.props.settings.nugetFeed,
          packages: this.convertEdgesToArray(this.props.settings.packages.edges, 'packages'),
          seedAddresses: this.props.settings.seedAddresses
        },
      }
    }
  }

  getOptimisticResponse () {
    return {
      model: {
        id: this.props.nodeId,
        settings: {
          migratorTemplates: this.props.settings.migratorTemplates,
          nodeTemplates: this.props.settings.nodeTemplates,
          nugetFeed: this.props.settings.nugetFeed,
          packages: this.props.settings.packages,
          seedAddresses: this.props.settings.seedAddresses,
          id: this.props.settings.id
        },
      },
    }
  }
}

