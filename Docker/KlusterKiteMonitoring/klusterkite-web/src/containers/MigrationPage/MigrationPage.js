import React from 'react'
import Relay from 'react-relay'

import delay from 'lodash/delay'

import NodesList from '../../components/NodesList/NodesList'
import MigrationLogs from '../../components/MigrationOperations/MigrationLogs'
import MigrationSteps from '../../components/MigrationOperations/MigrationSteps'
import NodesWithTemplates from '../../components/NodesWithTemplates/NodesWithTemplates'
import Warnings from '../../components/Warnings/Warnings';

import { hasPrivilege } from '../../utils/privileges'
import DateFormat from '../../utils/date'

class MigrationPage extends React.Component {

  static propTypes = {
    api: React.PropTypes.object,
    params: React.PropTypes.object,
  };

  static contextTypes = {
    router: React.PropTypes.object,
  };

  constructor (props) {
    super(props);
    this.state = {
      operationIsInProgress: false,
      migrationHasFinished: false,
      processErrors: null,
    };

    this.onStateChange = this.onStateChange.bind(this);
    this.onError = this.onError.bind(this);
  }

  componentDidMount = () => {
    delay(() => this.refetchDataOnTimer(), 5000);
  };

  componentWillUnmount = () => {
    clearTimeout(this._refreshId);
  };

  componentWillMount() {
    this.onReceiveProps(this.props);
  }

  componentWillReceiveProps(nextProps) {
    this.onReceiveProps(nextProps);
  }

  onReceiveProps(nextProps) {
    if (this.props.api && this.props.api.klusterKiteNodesApi.clusterManagement.currentMigration && !nextProps.api.klusterKiteNodesApi.clusterManagement.currentMigration) {
      // Migration just finshed
      console.log('Migration has been finished!');

      this.setState({
        migrationHasFinished: true,
      })
    }

    if (nextProps.api) {
      this.setState({
        operationIsInProgress: nextProps.api.klusterKiteNodesApi.clusterManagement.resourceState.operationIsInProgress
      });
    }
  }

  refetchDataOnTimer = () => {
    this.props.relay.forceFetch();
    this._refreshId = delay(() => this.refetchDataOnTimer(), 5000);
  };

  onStateChange() {
    this.setState({
      operationIsInProgress: true,
      processErrors: null,
    });
  }

  onError(errors) {
    this.setState({
      processErrors: errors,
    });
  }

  render () {
    const clusterManagement = this.props.api.klusterKiteNodesApi.clusterManagement;
    const currentMigration = clusterManagement.currentMigration;
    const resourceState = clusterManagement.resourceState;
    const nodesUpdating = resourceState.currentMigrationStep === 'NodesUpdating';

    return (
      <div>
        <Warnings
          klusterKiteNodesApi={this.props.api.klusterKiteNodesApi}
          migratableResourcesWarning={true}
          outOfScopeWarning={true}
        />

        {this.state.migrationHasFinished &&
          <div className="alert alert-success" role="alert">
            <span className="glyphicon glyphicon-ok" aria-hidden="true"></span>
            {' '}
            Migration finished successfully!
          </div>
        }

        {currentMigration &&
          <div>
            <h2>Migration {currentMigration.fromConfiguration && currentMigration.fromConfiguration.name} → {currentMigration.toConfiguration && currentMigration.toConfiguration.name}</h2>
            <p>Created: {currentMigration.started && DateFormat.formatDateTime(new Date(currentMigration.started))}</p>

            {(nodesUpdating || this.state.operationIsInProgress) && !this.state.migrationHasFinished &&
            <div className="alert alert-warning" role="alert">
              <span className="glyphicon glyphicon-time fa-spin" aria-hidden="true"></span>
              {' '}
              Operation in progress, please wait…
            </div>
            }

            {this.state.processErrors && this.state.processErrors.map((error, index) => {
              return (
                <div className="alert alert-danger" role="alert" key={`error-${index}`}>
                  <span className="glyphicon glyphicon-exclamation-sign" aria-hidden="true"></span>
                  {' '}
                  {error}
                </div>
              );
            })
            }

            {!nodesUpdating && !this.state.operationIsInProgress && !this.state.migrationHasFinished && resourceState.currentMigrationStep === 'Finish' && !resourceState.canFinishMigration &&
            <div className="alert alert-warning" role="alert">
              <span className="glyphicon glyphicon-time" aria-hidden="true"></span>
              {' '}
              All operations completed, but migration cannot be finished right now. Please wait.
            </div>
            }

            {!nodesUpdating && !this.state.operationIsInProgress && !this.state.migrationHasFinished && resourceState.currentMigrationStep === 'Start' && !resourceState.canCancelMigration &&
            <div className="alert alert-warning" role="alert">
              <span className="glyphicon glyphicon-time" aria-hidden="true"></span>
              {' '}
              Migration seems to be in init state, but cannot be cancelled right now.
            </div>
            }
          </div>
        }

        {!currentMigration &&
          <p>No active migration found.</p>
        }

        {currentMigration &&
          <MigrationSteps
            resourceState={resourceState}
            onStateChange={this.onStateChange}
            onError={this.onError}
            operationIsInProgress={this.state.operationIsInProgress}
          />
        }

        {currentMigration && resourceState.currentMigrationStep === 'NodesUpdating' &&
        <NodesWithTemplates data={this.props.api.klusterKiteNodesApi}/>
        }

        {resourceState.currentMigrationStep === 'NodesUpdating' &&
        <NodesList hasError={false}
                   upgradeNodePrivilege={hasPrivilege('KlusterKite.NodeManager.UpgradeNode')}
                   nodeDescriptions={this.props.api.klusterKiteNodesApi}
                   hideDetails={true}
        />
        }

        {currentMigration &&
          <MigrationLogs
            currentMigration={currentMigration}
          />
        }
      </div>
    )
  }
}

export default Relay.createContainer(
  MigrationPage,
  {
    fragments: {
      api: () => Relay.QL`
        fragment on IKlusterKiteNodeApi {
          klusterKiteNodesApi {
            clusterManagement {
              currentMigration {
                state
                started
                fromConfiguration {
                  name
                }
                toConfiguration {
                  name
                }
                ${MigrationLogs.getFragment('currentMigration')},
              }
              resourceState {
                operationIsInProgress
                canUpdateNodesToDestination
                canCancelMigration
                canFinishMigration
                canMigrateResources
                currentMigrationStep
                ${MigrationSteps.getFragment('resourceState')},
              }
            }
            ${NodesWithTemplates.getFragment('data')},
            ${NodesList.getFragment('nodeDescriptions')},
            ${Warnings.getFragment('klusterKiteNodesApi')},
          }
        }
      `,
    },
  },
)
