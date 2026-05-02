import React from 'react';
import Relay from 'react-relay'
import isEqual from 'lodash/isEqual';

import './styles.css';

export class UpdateResources extends React.Component {
  constructor(props) {
    super(props);

    this.state = {
      isProcessing: false,
      processSuccessful: false,
      processErrors: null,
      selectedResources: [],
      resourcesToUpgrade: [],
      resourcesToDowngrade: [],
    };
  }

  static propTypes = {
    migrationState: React.PropTypes.object,
    onStateChange: React.PropTypes.func.isRequired,
    onError: React.PropTypes.func.isRequired,
    operationIsInProgress: React.PropTypes.bool,
    canMigrateResources: React.PropTypes.bool,
    selectedResources: React.PropTypes.arrayOf(React.PropTypes.object),
    onSelectedResourcesChange: React.PropTypes.func,
  };

  componentWillMount() {
    this.onReceiveProps(this.props, true);
  }

  componentWillReceiveProps(nextProps) {
    this.onReceiveProps(nextProps, false);
  }

  onReceiveProps(nextProps, skipCheck) {
    if (nextProps.migrationState && (!isEqual(nextProps.migrationState, this.props.migrationState) || skipCheck)) {
      this.getOptionsList(nextProps.migrationState);
    }
  }

  getOptionsList(migrationState) {
    let resourcesToUpgrade = [];
    let resourcesToDowngrade = [];

    migrationState && migrationState.templateStates.edges && migrationState.templateStates.edges.forEach((edge) => {
      const node = edge.node;
      const migratableResources = migrationState.migratableResources.edges.map(edge => edge.node);
      node.migrators.edges.forEach((migratorEdge) => {
        const migratorNode = migratorEdge.node;
        const resources = migratorNode.resources.edges;
        resources.forEach((resourceEdge) => {
          const resourceNode = resourceEdge.node;
          const direction = (resourceNode.position === 'Source' || resourceNode.position === 'NotCreated') ? 'Destination' : 'Source';
          const isMigratable = migratableResources.some(x => x.key === resourceNode.key);
          const resource = {
            templateCode: node.code,
            migratorTypeName: migratorNode.typeName,
            resourceCode: resourceNode.code,
            target: direction,
            key: resourceNode.key,
          };

          if (isMigratable && resourceNode.migrationToDestinationExecutor !== null) {
            resourcesToUpgrade.push(resource);
          }
          if (isMigratable && resourceNode.migrationToSourceExecutor !== null) {
            resourcesToDowngrade.push(resource);
          }
        });
      });
    });

    this.setState({
      resourcesToUpgrade: resourcesToUpgrade,
      resourcesToDowngrade: resourcesToDowngrade
    });
  }

  onSelectResource(checked, key, templateCode, migratorTypeName, resourceCode, target) {
    const resource = {
      templateCode: templateCode,
      migratorTypeName: migratorTypeName,
      resourceCode: resourceCode,
      target: target,
      key: key,
    };

    if (checked) {
      const list = [
        ...this.props.selectedResources,
        resource,
      ];

      this.props.onSelectedResourcesChange(list);
    } else {
      const list = this.props.selectedResources.filter(item => item.key !== resource.key);
      this.props.onSelectedResourcesChange(list);
    }
  };

  onUpgradeAll(checked) {
    const keys = this.props.selectedResources.map(item => item.target === 'Destination' && item.key);
    const resourcesToAdd = this.state.resourcesToUpgrade.filter(item => !keys.includes(item.key));

    let list = [];
    if (checked) {
      list = [
        ...this.props.selectedResources,
        ...resourcesToAdd
      ];
    } else {
      list = this.props.selectedResources.filter(item => !keys.includes(item.key));
    }

    this.props.onSelectedResourcesChange(list);
  }

  onDowngradeAll(checked) {
    const keys = this.props.selectedResources.map(item => item.target === 'Source' && item.key);
    const resourcesToAdd = this.state.resourcesToDowngrade.filter(item => !keys.includes(item.key));

    let list = [];
    if (checked) {
      list = [
        ...this.props.selectedResources,
        ...resourcesToAdd
      ];
    } else {
      list = this.props.selectedResources.filter(item => !keys.includes(item.key));
    }

    this.props.onSelectedResourcesChange(list);
  }

  render() {
    const isProcessing = this.props.operationIsInProgress || this.state.isProcessing;

    return (
      <div>
        <h3>Resources list</h3>
        {isProcessing &&
        <div className="alert alert-warning" role="alert">
          <span className="glyphicon glyphicon-time fa-spin" aria-hidden="true"></span>
          {' '}
          Operation in progress, please wait…
        </div>
        }

        {this.props.migrationState && this.props.migrationState.templateStates.edges && this.props.migrationState.templateStates.edges.map((edge) => {
          const node = edge.node;
          const migratableResources = this.props.migrationState.migratableResources.edges.map(edge => edge.node);

          return (
            <div key={node.code}>
              <h4 className="migration-title">{node.code}</h4>
              <table className="table table-hover">
                <thead>
                <tr>
                  <th>Name</th>
                  <th>Code</th>
                  <th>Position</th>
                  <th>Current point</th>
                  <th className="migration-downgrade" title="Downgrade selected resources">
                    ↓<br />
                    <input type="checkbox" onChange={(element) => this.onDowngradeAll(element.target.checked)} />
                  </th>
                  <th className="migration-upgrade" title="Upgrade selected resources">
                    ↑<br />
                    <input type="checkbox" onChange={(element) => this.onUpgradeAll(element.target.checked)} />
                  </th>
                </tr>
                </thead>
                {node.migrators.edges.map((migratorEdge) => {
                  const migratorNode = migratorEdge.node;
                  const resources = migratorNode.resources.edges;

                  return (
                    <tbody key={migratorNode.typeName}>
                      <tr>
                        <th colSpan={5}>{migratorNode.name}</th>
                      </tr>
                      {resources.map((resourceEdge) => {
                        const resourceNode = resourceEdge.node;
                        const direction = (resourceNode.position === 'Source' || resourceNode.position === 'NotCreated') ? 'Destination' : 'Source';
                        const isMigratable = migratableResources.some(x => x.key === resourceNode.key);

                        return (
                          <tr key={resourceNode.code}>
                            <td className="migration-resources">{resourceNode.name}</td>
                            <td className="migration-resources">{resourceNode.code}</td>
                            <td className="migration-resources">{resourceNode.position}</td>
                            <td className="migration-resources">{resourceNode.currentPoint}</td>
                            <td className="migration-resources migration-downgrade">
                              {isMigratable && resourceNode.migrationToSourceExecutor !== null &&
                              <input
                                type="checkbox"
                                checked={this.props.selectedResources.some(item => item.target === direction && item.key === resourceNode.key)}
                                onChange={(element) => this.onSelectResource(element.target.checked, resourceNode.key, node.code, migratorNode.typeName, resourceNode.code, direction)}
                                disabled={isProcessing}
                              />
                              }
                            </td>
                            <td className="migration-resources migration-upgrade">
                              {isMigratable && resourceNode.migrationToDestinationExecutor !== null &&
                                <input
                                  type="checkbox"
                                  checked={this.props.selectedResources.some(item => item.target === direction && item.key === resourceNode.key)}
                                  onChange={(element) => this.onSelectResource(element.target.checked, resourceNode.key, node.code, migratorNode.typeName, resourceNode.code, direction)}
                                  disabled={isProcessing}
                                />
                              }
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  )
                })}
              </table>
            </div>
          )
        })}
      </div>
    );
  }
}

export default Relay.createContainer(
  UpdateResources,
  {
    fragments: {
      migrationState: () => Relay.QL`fragment on IKlusterKiteNodeApi_MigrationActorMigrationState {
        templateStates   {
          edges {
            node {
              code
              position
              migrators {
                edges {
                  node {
                    typeName
                    name
                    position
                    direction
                    resources {
                      edges {
                        node {
                          key
                          sourcePoint
                          destinationPoint
                          position
                          migrationToSourceExecutor
                          migrationToDestinationExecutor
                          name
                          code
                          currentPoint
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        migratableResources {
          edges {
            node {
              key,
            }
          }
        }
      }
      `,
    },
  },
)

