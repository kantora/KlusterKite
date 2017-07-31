import React from 'react';
import Relay from 'react-relay'

import DraftOperations from './DraftOperations';
import ReadyOperations from './ReadyOperations';
import ActiveOperations from './ActiveOperations';
import ArchivedOperations from './ArchivedOperations';
import ObsoleteOperations from './ObsoleteOperations';

import './styles.css';

export class ConfigurationOperations extends React.Component {
  constructor(props) {
    super(props);

    this.state = {
      isChecking: false,
      checkSuccess: false,
      checkCompatibleTemplates: null,
      checkActiveNodes: null,
      isSettingReady: false,
      isUpdating: false,
      isChangingState: false
    };
  }

  static propTypes = {
    settings: React.PropTypes.object,
    nodeManagement: React.PropTypes.object,
    isStable: React.PropTypes.bool.isRequired,
    configurationId: React.PropTypes.string.isRequired,
    configurationInnerId: React.PropTypes.number.isRequired,
    currentState: React.PropTypes.string.isRequired,
    onForceFetch: React.PropTypes.func.isRequired,
    onStartMigration: React.PropTypes.func.isRequired,
  };

  getErrorMessagesFromEdge = (edges) => {
    return edges.map(x => x.node).map(x => x.message);
  };

  /**
   * Checks if there is any resource in non Source position
   * @return {boolean} Resource in non Source position found
   */
  hasNonSourceResource() {
    const resourceState = this.props.nodeManagement.resourceState;
    let error = false;
    if (resourceState && resourceState.configurationState && resourceState.configurationState.states && resourceState.configurationState.states.edges) {
      const configurationStateNodes = resourceState.configurationState.states.edges.map(x => x.node);
      configurationStateNodes.forEach(configurationStateNode => {
        if (configurationStateNode.migratorsStates.edges.length > 0) {
          const migratorsStatesNodes = configurationStateNode.migratorsStates.edges.map(x => x.node);
          migratorsStatesNodes.forEach(migratorsStatesNode => {
            if (migratorsStatesNode.resources.edges.length > 0){
              error = true;
            }
          });
        }
      });
    }

    return error;
  }

  /**
   * Checks if there is any resource in obsolete position
   * @return {boolean} Resource in obsolete position
   */
  isResourseObsolete() {
    return this.props.klusterKiteNodesApi.getActiveNodeDescriptions.edges.length > 0;
  }

  render() {
    const nodeTemplates = this.props.settings && this.props.settings.nodeTemplates && this.props.settings.nodeTemplates.edges;

    return (
      <div>
        {this.props.currentState !== 'Active' &&
          <h2>Operations</h2>
        }
        <div>
          <DraftOperations
            nodeTemplates={nodeTemplates}
            configurationId={this.props.configurationId}
            configurationInnerId={this.props.configurationInnerId}
            currentState={this.props.currentState}
            onForceFetch={this.props.onForceFetch}
          />

          <ReadyOperations
            configurationId={this.props.configurationId}
            configurationInnerId={this.props.configurationInnerId}
            currentState={this.props.currentState}
            onForceFetch={this.props.onForceFetch}
            canCreateMigration={this.props.nodeManagement.resourceState.canCreateMigration}
            currentMigration={this.props.nodeManagement.currentMigration}
            onStartMigration={this.props.onStartMigration}
            operationIsInProgress={this.props.nodeManagement.resourceState.operationIsInProgress}
            resourceInNonSourcePosition={this.hasNonSourceResource()}
            resourceIsObsolete={this.isResourseObsolete()}
          />

          <ActiveOperations
            configurationId={this.props.configurationId}
            configurationInnerId={this.props.configurationInnerId}
            currentState={this.props.currentState}
            onForceFetch={this.props.onForceFetch}
            isStable={this.props.isStable}
          />

          <ArchivedOperations
            configurationId={this.props.configurationId}
            configurationInnerId={this.props.configurationInnerId}
            currentState={this.props.currentState}
            onForceFetch={this.props.onForceFetch}
            canCreateMigration={this.props.nodeManagement.resourceState.canCreateMigration}
            currentMigration={this.props.nodeManagement.currentMigration}
            onStartMigration={this.props.onStartMigration}
          />

          <ObsoleteOperations
            currentState={this.props.currentState}
          />
        </div>
        <h2 className="margined-header">Settings</h2>

        {this.state.isChangingState &&
        <div className="alert alert-warning" role="alert">
          <span className="glyphicon glyphicon-time" aria-hidden="true"></span>
          {' '}
          Please wait, expecting server reply…
        </div>
        }
      </div>
    );
  }
}

export default Relay.createContainer(
  ConfigurationOperations,
  {
    fragments: {
      settings: () => Relay.QL`fragment on IKlusterKiteNodeApi_ConfigurationSettings {
        nodeTemplates {
          edges {
            node {
              code
            }
          }
        }
      }`,
      nodeManagement: () => Relay.QL`fragment on IKlusterKiteNodeApi_ClusterManagement {
        resourceState {
          canCreateMigration
          operationIsInProgress
          configurationState {
            states {
              edges {
                node {
                  migratorsStates {
                    edges {
                      node {
                        resources(filter: { position_not: Source }) {
                          edges {
                            node {
                              name
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        currentMigration {
          state
        }
      }`,
      klusterKiteNodesApi: () => Relay.QL`fragment on KlusterKiteNodeApi_Root {
        getActiveNodeDescriptions(filter: { isObsolete: true }) {
          edges {
            node {
              id
            }
          }
        }
      }`,
    },
  },
)
