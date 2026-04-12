import React from 'react';
import Relay from 'react-relay'

import delay from 'lodash/delay'

import { Popover, OverlayTrigger, Button } from 'react-bootstrap';
import Icon from 'react-fa';

import UpgradeNodeMutation from './mutations/UpgradeNodeMutation';

import './styles.css';

export class NodesList extends React.Component {

  constructor(props) {
    super(props);
    this.nodePopover = this.nodePopover.bind(this);

    this.state = {
      upgradingNodes: []
    };
  }

  static propTypes = {
    nodeDescriptions: React.PropTypes.object,
    hasError: React.PropTypes.bool.isRequired,
    upgradeNodePrivilege: React.PropTypes.bool.isRequired,
    testMode: React.PropTypes.bool,
    hideDetails: React.PropTypes.bool,
  };

  drawRole(node, role) {
    const isLeader = node.leaderInRoles.indexOf(role) >= 0;
    return (<span key={`${node.NodeId}/${role}`}>
                {isLeader && <span className="label label-info" title={`${role} leader`}>{role}</span>}
                {!isLeader && <span className="label label-default">{role}</span>}
                {' '}
    </span>);
  }

  nodePopover(node) {
    return (
      <Popover title={`${node.nodeTemplate}`} id={`${node.nodeId}`}>
        {node.modules.edges.map((subModuleEdge) => {
          const subModuleNode =  subModuleEdge.node;
          return (
            <span key={`${subModuleNode.id}`}>
              <span className="label label-default">{subModuleNode._id}&nbsp;{subModuleNode.version}</span>{' '}
            </span>
          );
        })
        }
      </Popover>
    );
  }

  onManualUpgrade(nodeAddress, nodeId) {
    if (this.props.testMode) {
      this.showUpgrading(nodeId);
      this.hideUpgradingAfterDelay(nodeId);
    } else {
      Relay.Store.commitUpdate(
        new UpgradeNodeMutation({address: nodeAddress}),
        {
          onSuccess: (response) => {
            const result = response.klusterKiteNodeApi_klusterKiteNodesApi_upgradeNode.result && response.klusterKiteNodeApi_klusterKiteNodesApi_upgradeNode.result.result;
            if (result) {
              this.showUpgrading(nodeId);
              this.hideUpgradingAfterDelay(nodeId);
            } else {
              this.showErrorMessage();
              this.hideErrorMessageAfterDelay();
            }
          },
          onFailure: (transaction) => console.log(transaction),
        },
      )
    }
  }

  showUpgrading(nodeId) {
    this.setState((prevState, props) => ({
      upgradingNodes: [...prevState.upgradingNodes, nodeId]
    }));
  }

  hideUpgrading(nodeId) {
    this.setState(function(prevState, props) {
      const index = prevState.upgradingNodes.indexOf(nodeId);
      return {
        upgradingNodes: [
          ...prevState.upgradingNodes.slice(0, index),
          ...prevState.upgradingNodes.slice(index + 1)
        ]
      };
    });
  }

  hideUpgradingAfterDelay(nodeId) {
    delay(() => this.hideUpgrading(nodeId), 20000);
  }

  /**
   * Shows reloading packages message
   */
  showErrorMessage = () => {
    this.setState({
      isError: true
    });
  };

  /**
   * Hides reloading packages message after delay
   */
  hideErrorMessageAfterDelay = () => {
    delay(() => this.hideErrorMessage(), 5000);
  };

  /**
   * Hides reloading packages message
   */
  hideErrorMessage = () => {
    this.setState({
      isError: false
    });
  };

  render() {
    if (!this.props.nodeDescriptions.getActiveNodeDescriptions){
      return (<div></div>);
    }
    let { hasError } = this.props;
    if (this.state.isError) {
      hasError = true;
    }
    const edges = this.props.nodeDescriptions.getActiveNodeDescriptions.edges;

    return (
      <div>
        <h3>Nodes list</h3>
        {hasError &&
          <div className="alert alert-danger" role="alert">
            <span className="glyphicon glyphicon-exclamation-sign" aria-hidden="true"></span>
            <span> Could not connect to the server</span>
          </div>
        }
        <table className="table table-hover">
          <thead>
            <tr>
              <th>Leader</th>
              <th>Address</th>
              <th>Template</th>
              <th>Container</th>
              {!this.props.hideDetails &&
                <th>Modules</th>
              }
              {!this.props.hideDetails &&
                <th>Roles</th>
              }
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
          {edges && edges.map((edge) => {
            const node = edge.node;
            const isUpdating = this.state.upgradingNodes.indexOf(node.nodeId) !== -1;
            const reloadClassName = isUpdating ? 'fa fa-refresh fa-spin' : 'fa fa-refresh';
            return (
              <tr key={`${node.nodeId}`}>
                <td>{node.isClusterLeader ? <i className="fa fa-check-circle" aria-hidden="true"></i> : ''}</td>
                <td>{node.nodeAddress.host}:{node.nodeAddress.port}</td>
                <td>
                  {node.nodeTemplate}
                </td>
                <td>
                  {node.containerType}
                </td>
                {!this.props.hideDetails &&
                  <td>
                    {node.isInitialized &&
                    <OverlayTrigger trigger="click" rootClose placement="bottom" overlay={this.nodePopover(node)}>
                      <Button className="btn-info btn-xs">
                        <Icon name="search"/>
                      </Button>
                    </OverlayTrigger>
                    }
                  </td>
                }
                {!this.props.hideDetails &&
                  <td>
                    {node.roles.map((role) => this.drawRole(node, role))}
                  </td>
                }
                {node.isInitialized &&
                <td>
                  <span className="label">{node.isInitialized}</span>
                  {this.props.upgradeNodePrivilege &&
                    <span>
                      {!node.isObsolete &&
                        <button
                          disabled={isUpdating}
                          type="button" className="upgrade btn btn-xs btn-success"
                          title="Upgrade Node"
                          onClick={() => this.onManualUpgrade(node.nodeAddress.asString, node.nodeId)}>
                          <i className={reloadClassName} /> Actual
                        </button>
                      }
                      {node.isObsolete &&
                        <button
                          disabled={isUpdating}
                          type="button" className="upgrade btn btn-xs btn-warning"
                          title="Upgrade Node"
                          onClick={() => this.onManualUpgrade(node.nodeAddress.asString, node.nodeId)}>
                          <i className={reloadClassName} /> Obsolete
                        </button>
                      }
                    </span>
                  }
                  {!this.props.upgradeNodePrivilege &&
                  <span>
                    {!node.isObsolete &&
                    <span className="label label-success">Actual</span>
                    }
                    {node.isObsolete &&
                    <span className="label label-warning">Obsolete</span>
                    }
                  </span>
                  }
                </td>
                }
                {!node.isInitialized &&
                <td>
                  <span className="label label-info">Uncontrolled</span>
                </td>
                }
              </tr>
            )
          })
          }
          </tbody>
        </table>

      </div>
    );
  }
}

export default Relay.createContainer(
  NodesList,
  {
    fragments: {
      nodeDescriptions: () => Relay.QL`fragment on IKlusterKiteNodeApi_Root {
        getActiveNodeDescriptions
        {
          edges {
            node {
              containerType,
              isClusterLeader,
              isObsolete,
              isInitialized,
              leaderInRoles,
              nodeId,
              nodeTemplate,
              roles,
              startTimeStamp,
              nodeAddress {
                host,
                port,
                asString,
              },
              modules {
                edges {
                  node {
                    id,
                    _id,
                    version,
                  }
                }
              },
            }
          }
        }
      }
      `,
    },
  },
)


