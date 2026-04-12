import React from 'react'
import Relay from 'react-relay'
import { browserHistory } from 'react-router'

import CloneConfigMutation from './mutations/CloneConfigMutation'

class ConfigurationConfigCopyPage extends React.Component {

  static propTypes = {
    api: React.PropTypes.object,
    params: React.PropTypes.object,
  };

  constructor (props) {
    super(props);
    this.state = {
      processing: false,
      oldConifgNotFoundError: false
    }
  }

  componentWillMount() {
    this.copyConfiguration();
  }

  convertNameToId = (nugetPackages) => {
    // Temporary hack while nugetPackages _id is incorrect
    nugetPackages.edges.forEach((item => {
      item.node._id = item.node.name;
      delete item.node.name;
    }));

    return nugetPackages;
  };

  copyConfiguration = () => {
    if (this.props.api.klusterKiteNodesApi.configurations.edges && this.props.api.klusterKiteNodesApi.configurations.edges.length > 0){
      let oldSettings = this.props.api.klusterKiteNodesApi.configurations.edges[0].node.settings;
      if (this.props.params.mode === 'updateCurrent') {
        oldSettings = this.props.api.configuration.settings;
      }

      if (this.props.params.mode === 'update' || this.props.params.mode === 'updateCurrent') {
        oldSettings.packages = this.convertNameToId(this.props.api.klusterKiteNodesApi.nugetPackages);
      }

      this.setState({
        processing: true
      });

      this.cloneConfig(oldSettings);
    } else {
      this.setState({
        oldConifgNotFoundError: true
      });
    }
  };

  cloneConfig = (setting) => {
    Relay.Store.commitUpdate(
      new CloneConfigMutation(
        {
          nodeId: this.props.params.configurationId,
          configurationId: this.props.api.configuration._id,
          settings: setting,
        }),
      {
        onSuccess: (response) => {
          console.log('response', response);
          if (response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors &&
            response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors.edges);

            this.setState({
              processing: false,
              saveErrors: messages
            });
          } else {
            this.setState({
              processing: false,
              saveErrors: null
            });
            browserHistory.push(`/klusterkite/Configuration/${this.props.params.configurationId}`);
          }
        },
        onFailure: (transaction) => {
          this.setState({
            processing: false
          });
          console.log(transaction)},
      },
    )
  };

  getErrorMessagesFromEdge = (edges) => {
    return edges.map(x => x.node).map(x => x.message);
  };

  render () {
    return (
      <div>
        {this.state.oldConifgNotFoundError &&
          <div>
            <h2>Error!</h2>
            <p>Old configuration is not found.</p>
          </div>
        }
        {this.state.processing &&
          <div>
            <h2>Copying config</h2>
            <p>Please wait…</p>
          </div>
        }
      </div>
    )
  }
}

const settingsFragment = Relay.QL`
  fragment on IKlusterKiteNodeApi_ConfigurationSettings {
    nugetFeed
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
          packagesToInstall {
            edges {
              node {
                key
                value {
                  edges {
                    node {
                      _id
                      version
                    }
                  }
                }
              }
            }
          }
          priority
        }
      }
    }
    packages {
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
`;

export default Relay.createContainer(
  ConfigurationConfigCopyPage,
  {
    initialVariables: {
      configurationId: null,
      nodeExists: false,
    },
    prepareVariables: (prevVariables) => Object.assign({}, prevVariables, {
      nodeExists: prevVariables.configurationId !== null,
    }),
    fragments: {
      api: () => Relay.QL`        
        fragment on IKlusterKiteNodeApi {
          id
          klusterKiteNodesApi {
            configurations(filter: { state: Active }, limit: 1) {
              edges {
                node {
                  settings {
                    ${settingsFragment}
                  }
                }
              }
            }
            nugetPackages {
              edges {
                node {
                  _id
                  name
                  version
                }
              }
            }
          }
          configuration: node(id: $configurationId) @include( if: $nodeExists ) {
            ...on IKlusterKiteNodeApi_Configuration {
              _id
              name
              notes
              minorVersion
              majorVersion
              state
              settings {
                ${settingsFragment}
              }
            }
          }
        }
      `,
    },
  },
)
