import React from 'react';
import Relay from 'react-relay'

import UpdateResources from '../../components/MigrationOperations/UpdateResources'
import UpdateResourcesButton from '../../components/MigrationOperations/UpdateResourcesButton'

import CancelMigration from '../../components/MigrationOperations/CancelMigration'
import FinishMigration from '../../components/MigrationOperations/FinishMigration'
import UpdateNodes from '../../components/MigrationOperations/UpdateNodes'

import './styles.css';

export class MigrationSteps extends React.Component {
  constructor(props) {
    super(props);
    this.onSelectedResourcesChange = this.onSelectedResourcesChange.bind(this);

    this.state = {
      migrationSteps: null,
      currentMigrationStep: null,
      selectedResources: [],
    };

    this.replacements = {
      NodesUpdating: 'Updating Nodes',
      NodesUpdated: 'Nodes Updated',
      ResourcesUpdating: 'Updating Resources',
      ResourcesUpdated: 'Resources Updated',
      PreNodesResourcesUpdating: 'Updating Resources (Pre)',
      PreNodeResourcesUpdated: 'Resources Updated (Pre)',
      PostNodesResourcesUpdating: 'Updating Resources (Post)',
    };
  }

  static propTypes = {
    resourceState: React.PropTypes.object,
    onStateChange: React.PropTypes.func.isRequired,
    onError: React.PropTypes.func.isRequired,
    operationIsInProgress: React.PropTypes.bool,
  };

  componentWillMount() {
    this.onReceiveProps(this.props);
  }

  componentWillReceiveProps(nextProps) {
    this.onReceiveProps(nextProps);
  }

  onReceiveProps(nextProps) {
    // We are mapping those props into state because we want to cache them in case of server downtime
    if (nextProps.resourceState.migrationSteps && nextProps.resourceState.migrationSteps.length > 0) {
      this.setState({
        migrationSteps: nextProps.resourceState.migrationSteps
      });
    }

    if (nextProps.resourceState.currentMigrationStep) {
      this.setState({
        currentMigrationStep: nextProps.resourceState.currentMigrationStep
      });
    }
  }

  onSelectedResourcesChange(selectedResources) {
    this.setState({
      selectedResources: selectedResources
    });
  }

  render() {
    const migrationSteps = this.state.migrationSteps;
    const currentMigrationStep = this.state.currentMigrationStep;
    const activeIndex = migrationSteps ? migrationSteps.indexOf(currentMigrationStep) : -1;
    const lastIndex = migrationSteps ? migrationSteps.length - 1 : -1;
    const nodesUpdating = currentMigrationStep === 'NodesUpdating';
    const operationIsInProgress = nodesUpdating || this.props.operationIsInProgress || this.props.resourceState.operationIsInProgress;

    return (
      <div>
        {migrationSteps &&
          <div className="panel panel-default">
            <div className="panel-body">

              <ul className="migration-steps">
                {migrationSteps.map((step, index) => {
                  const className = index === activeIndex ? 'active' : '';
                  const classNameHrLeft = index === 0 ? 'empty' : (index <= activeIndex ? 'active' : '');
                  const classNameHrRight = index === lastIndex ? 'empty' : (activeIndex > index ? 'active' : '');
                  const title = this.replacements[step] ? this.replacements[step] : step;

                  return (
                    <li key={step} className={className}>
                      <hr className={classNameHrLeft}/>
                      <hr className={classNameHrRight}/>
                      <div>
                        <span className="index">{index + 1}</span>
                      </div>
                      <p className="title">{title}</p>
                    </li>
                  )
                })}
              </ul>
              <div className="migration-controls-outer">
                <div className="migration-controls migration-controls-left">
                  <CancelMigration
                    onStateChange={this.props.onStateChange}
                    onError={this.props.onError}
                    canCancelMigration={this.props.resourceState.canCancelMigration}
                    operationIsInProgress={operationIsInProgress}
                  />

                  <UpdateNodes
                    onStateChange={this.props.onStateChange}
                    onError={this.props.onError}
                    canUpdateBackward={this.props.resourceState.canUpdateNodesToSource}
                    operationIsInProgress={operationIsInProgress}
                  />

                  {(currentMigrationStep === 'ResourcesUpdated' || currentMigrationStep === 'Finish' || currentMigrationStep === 'PreNodesResourcesUpdating' || currentMigrationStep === 'PreNodeResourcesUpdated') &&
                  <UpdateResourcesButton
                    onStateChange={this.props.onStateChange}
                    onError={this.props.onError}
                    migrationState={this.props.resourceState.migrationState}
                    canMigrateResources={this.props.resourceState.canMigrateResources}
                    operationIsInProgress={operationIsInProgress}
                    selectedResources={this.state.selectedResources}
                    onSelectedResourcesChange={this.onSelectedResourcesChange}
                    direction="downgrade"
                  />
                  }
                </div>
                <div className="migration-controls migration-controls-right">
                  <UpdateNodes
                    onStateChange={this.props.onStateChange}
                    onError={this.props.onError}
                    canUpdateForward={this.props.resourceState.canUpdateNodesToDestination}
                    operationIsInProgress={operationIsInProgress}
                  />

                  <FinishMigration
                    onStateChange={this.props.onStateChange}
                    onError={this.props.onError}
                    canFinishMigration={this.props.resourceState.canFinishMigration}
                    operationIsInProgress={operationIsInProgress}
                  />

                  {(currentMigrationStep === 'Start' || currentMigrationStep === 'PreNodesResourcesUpdating' || currentMigrationStep === 'NodesUpdated') &&
                  <UpdateResourcesButton
                    onStateChange={this.props.onStateChange}
                    onError={this.props.onError}
                    migrationState={this.props.resourceState.migrationState}
                    canMigrateResources={this.props.resourceState.canMigrateResources}
                    operationIsInProgress={operationIsInProgress}
                    selectedResources={this.state.selectedResources}
                    onSelectedResourcesChange={this.onSelectedResourcesChange}
                    direction="upgrade"
                  />
                  }
                </div>
              </div>
            </div>
          </div>
        }

        <UpdateResources
          onStateChange={this.props.onStateChange}
          onError={this.props.onError}
          migrationState={this.props.resourceState.migrationState}
          canMigrateResources={this.props.resourceState.canMigrateResources}
          operationIsInProgress={operationIsInProgress}
          selectedResources={this.state.selectedResources}
          onSelectedResourcesChange={this.onSelectedResourcesChange}
        />
      </div>
    );
  }
}

export default Relay.createContainer(
  MigrationSteps,
  {
    fragments: {
      resourceState: () => Relay.QL`fragment on IKlusterKiteNodeApi_ResourceState {
        operationIsInProgress
        canUpdateNodesToDestination
        canUpdateNodesToSource
        canCancelMigration
        canFinishMigration
        canMigrateResources
        migrationSteps
        currentMigrationStep
        migrationState {
          ${UpdateResources.getFragment('migrationState')},
        }
      }
      `,
    },
  },
)
