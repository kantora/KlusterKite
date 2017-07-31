import React from 'react';
import Relay from 'react-relay'
import Icon from 'react-fa';

import UpdateNodesMutation from './mutations/UpdateNodesMutation';

export class UpdateNodes extends React.Component {
  constructor(props) {
    super(props);

    this.state = {
      isProcessing: false,
      processSuccessful: false,
      processErrors: null,
    };
  }

  static propTypes = {
    canUpdateForward: React.PropTypes.bool,
    canUpdateBackward: React.PropTypes.bool,
    onStateChange: React.PropTypes.func.isRequired,
    onError: React.PropTypes.func.isRequired,
    operationIsInProgress: React.PropTypes.bool,
  };

  onStartUpdateDestination = () => {
    return this.onStartUpdate('Destination');
  }

  onStartUpdateSource = () => {
    return this.onStartUpdate('Source');
  }

  onStartUpdate = (target) => {
    if (!this.state.isProcessing){

      this.setState({
        isProcessing: true,
        processSuccessful: false,
      });

      Relay.Store.commitUpdate(
        new UpdateNodesMutation({
          target: target,
        }),
        {
          onSuccess: (response) => {
            const responsePayload = response.klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_migrationNodesUpdate;

            if (responsePayload.errors &&
              responsePayload.errors.edges) {
              const messages = this.getErrorMessagesFromEdge(responsePayload.errors.edges);
              this.props.onError(messages);

              this.setState({
                processSuccessful: false,
                processErrors: messages,
              });
            } else {
              // total success
              this.setState({
                isProcessing: false,
                processErrors: null,
                processSuccessful: true,
              });

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

  getErrorMessagesFromEdge = (edges) => {
    return edges.map(x => x.node).map(x => x.message);
  };

  render() {
    let processClassName = '';
    if (this.state.isProcessing) {
      processClassName += ' fa-spin';
    }

    return (
    <div>
      {this.props.canUpdateForward && !this.state.isProcessing && !this.props.operationIsInProgress &&
        <button className="btn btn-primary" type="button" onClick={this.onStartUpdateDestination}>
          <Icon name="forward" className={processClassName}/>{' '}Update nodes
        </button>
      }

      {this.props.canUpdateBackward && !this.state.isProcessing && !this.props.operationIsInProgress &&
      <button className="btn btn-primary" type="button" onClick={this.onStartUpdateSource}>
        <Icon name="backward" className={processClassName}/>{' '}Rollback nodes
      </button>
      }
    </div>
    );
  }
}

export default UpdateNodes
