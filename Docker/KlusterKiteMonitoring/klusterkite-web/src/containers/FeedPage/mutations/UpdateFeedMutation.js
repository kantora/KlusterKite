import Relay from 'react-relay'

export default class UpdateFeedMutation extends Relay.Mutation {
  static fragments = {
    settings: () => Relay.QL`
      fragment on IKlusterKiteNodeApi_ConfigurationSettings {
        nodeTemplates {
          edges {
            node {
              id
              code
              configuration
              containerTypes
              minimumRequiredInstances
              maximumNeededInstances
              name
              packageRequirements {
                edges {
                  node {
                    _id
                    specificVersion
                  }
                }
              }
              priority
            }
          }
        }
        migratorTemplates {
          edges {
            node {
              id
              code
              configuration
              name
              notes
              packageRequirements {
                edges {
                  node {
                    _id
                    specificVersion
                  }
                }
              }
              priority
            }
          }
        }
        nugetFeed
        packages(sort: id_asc) {
          edges {
            node {
              version
              id
              _id
            }
          }
        }
        seedAddresses
        id
      }
    `,
  };

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
    const typeSingular = type.substring(0, type.length - 1);

    // We iterate over old edge and search for updates of every record in this.props[typeSingular]
    // Also we change id's (from _id to id) because ID types are different in input and output
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

      // Updating a record
      if (this.props[typeSingular] && this.props[`${typeSingular}Id`] === node.id) {
        newNode = this.props[typeSingular];
      }

      // Delete a record
      if (this.props[typeSingular] && this.props[`${typeSingular}DeleteId`] === node.id) {
        newNode = null
      }

      if (this.props['migratorTemplateDeleteId'] && this.props['migratorTemplateDeleteId'] === node.id) {
        console.log('trying to delete ', this.props['migratorTemplateDeleteId']);
      }

      if (newNode) {
        nodes.push(newNode);
      }
    });

    // Adding a record
    if (this.props[typeSingular]) {
      console.log('Creating or updating', this.props[`${typeSingular}Id`]);
    }
    if (this.props[typeSingular] && this.props[`${typeSingular}Id`] === null) {
      nodes.push(this.props[typeSingular]);
    }

    // seedAddresses are passed as an simple string array, without id's or anything
    if (this.props[typeSingular] === 'seedAddresses') {
      nodes = this.props[typeSingular];
    }

    // packagesList is passed as a simple object array, without id's
    if (type === 'packages' && this.props['packagesList']) {
      nodes = this.props['packagesList'];
    }

    return nodes;
  }

  /**
   * Checks if seed addresses are being updated and return old / updated list as needed
   * @param seedAddresses {string[]} Seed addresses list
   * @return {string[]} Updated seed addresses list
   */
  updateSeedAddresses(seedAddresses) {
    if (this.props.seedAddresses) {
      return this.props.seedAddresses;
    }

    return seedAddresses;
  }

  getVariables () {
    return {
      id: this.props.configurationId,
      newNode: {
        id: this.props.configurationId,
        settings: {
          nodeTemplates: this.convertEdgesToArray(this.props.settings.nodeTemplates.edges, 'nodeTemplates'),
          migratorTemplates: this.convertEdgesToArray(this.props.settings.migratorTemplates.edges, 'migratorTemplates'),
          nugetFeed: this.props.nugetFeed || this.props.settings.nugetFeed,
          packages: this.convertEdgesToArray(this.props.settings.packages.edges, 'packages'),
          seedAddresses: this.updateSeedAddresses(this.props.settings.seedAddresses),
        },
      }
    }
  }

  getOptimisticResponse () {
    return {
      model: {
        id: this.props.nodeId,
        settings: {
          nodeTemplates: this.props.settings.nodeTemplates,
          migratorTemplates: this.props.settings.migratorTemplates,
          nugetFeed: this.props.settings.nugetFeed,
          packages: this.props.settings.packages,
          seedAddresses: this.props.settings.seedAddresses,
          id: this.props.settings.id
        },
      },
    }
  }
}

