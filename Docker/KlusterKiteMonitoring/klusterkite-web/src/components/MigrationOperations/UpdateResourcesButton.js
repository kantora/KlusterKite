import React from 'react';
import Relay from 'react-relay'
import Icon from 'react-fa';

import UpdateResourcesMutation from './mutations/UpdateResourcesMutation';

import './styles.css';

export class UpdateResourcesButton extends React.Component {
  constructor(props) {
    super(props);

    this.state = {
      isProcessing: false,
      processSuccessful: false,
      processErrors: null,
    };
  }

  static propTypes = {
    migrationState: React.PropTypes.object,
    onStateChange: React.PropTypes.func.isRequired,
    onError: React.PropTypes.func.isRequired,
    operationIsInProgress: React.PropTypes.bool,
    canMigrateResources: React.PropTypes.bool,
    selectedResources: React.PropTypes.arrayOf(React.PropTypes.object).isRequired,
    onSelectedResourcesChange: React.PropTypes.func.isRequired,
    direction: React.PropTypes.string.isRequired,
  };

  onStartUpdateDestination = () => {
    return this.onStartUpdate('Destination');
  };

  onStartUpdateSource = () => {
    return this.onStartUpdate('Source');
  };

  onStartMassMigration = () => {
    if (!this.state.isProcessing){
      this.setState({
        isProcessing: true,
        processSuccessful: false,
      });

      Relay.Store.commitUpdate(
        new UpdateResourcesMutation({
          resources: this.prepareResourceListForMigration(this.props.selectedResources)
        }),
        {
          onSuccess: (response) => {
            const responsePayload = response.klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationResourceUpdate;

            if (responsePayload.errors &&
              responsePayload.errors.edges) {
              const messages = this.getErrorMessagesFromEdge(responsePayload.errors.edges);
              this.props.onError(messages);

              this.setState({
                processSuccessful: false,
                processErrors: messages,
              });
            } else {
              // console.log('result update nodes', responsePayload.result);
              // total success
              this.setState({
                isProcessing: false,
                processErrors: null,
                processSuccessful: true,
              });

              this.props.onSelectedResourcesChange([]);

              this.props.onStateChange();
            }
          },
          onFailure: (transaction) => {
            this.setState({
              isProcessing: false
            });
            console.log(transaction)},
        },
      );
    }
  };

  /**
   * Prepare resource list for migration by removal unnecessary keys
   * @param resources {Object[]} Resources List
   * @return {Object[]} Prepared resources list
   */
  prepareResourceListForMigration = (resources) => {
    let resourceList = [];

    resources.forEach((item) => {
      resourceList.push({
        templateCode: item.templateCode,
        migratorTypeName: item.migratorTypeName,
        resourceCode: item.resourceCode,
        target: item.target,
      })
    });

    return resourceList;
  };

  onSelectResource = (checked, key, templateCode, migratorTypeName, resourceCode, target) => {
    const resource = {
      templateCode: templateCode,
      migratorTypeName: migratorTypeName,
      resourceCode: resourceCode,
      target: target,
      key: key,
    };

    if (checked) {
      this.setState((prevState) => ({
        selectedResources: [
          ...prevState.selectedResources,
          resource,
        ],
      }));
    } else {
      this.setState((prevState) => ({
        selectedResources: prevState.selectedResources.filter(item => item.key !== resource.key),
      }));
    }

  };

  getErrorMessagesFromEdge = (edges) => {
    return edges.map(x => x.node).map(x => x.message);
  };

  render() {
    const upgradePossible = this.props.selectedResources.some(item => item.target === 'Destination');
    const downgradePossible = this.props.selectedResources.some(item => item.target === 'Source');
    const isProcessing = this.props.operationIsInProgress || this.state.isProcessing;

    return (
      <div>
        {this.props.canMigrateResources && ((upgradePossible && this.props.direction === 'upgrade') || (downgradePossible && this.props.direction === 'downgrade') || (!upgradePossible && !downgradePossible)) &&
        <button className="btn btn-primary" type="button" onClick={() => {this.onStartMassMigration()}} disabled={isProcessing || (!upgradePossible && !downgradePossible)}>
          <Icon name='forward' />{' '}
          {upgradePossible && !downgradePossible && <span>Upgrade</span>}
          {downgradePossible && !upgradePossible && <span>Downgrade</span>}
          {!upgradePossible && !downgradePossible && <span>Process</span>}
          {downgradePossible && upgradePossible && <span>Upgrade and downgrade</span>}
          {' '}selected{' '}
          {this.props.selectedResources.length === 1 && <span>resouce</span>}
          {this.props.selectedResources.length !== 1 && <span>resouces</span>}
        </button>
        }
      </div>
    );
  }
}

export default UpdateResourcesButton
