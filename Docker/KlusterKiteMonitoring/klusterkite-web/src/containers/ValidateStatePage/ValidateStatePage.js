import React from 'react'
import Relay from 'react-relay'
import { browserHistory } from 'react-router'

import ValidateStateMutation from './mutations/ValidateStateMutation'

class ValidateStatePage extends React.Component {

  static propTypes = {
    api: React.PropTypes.object,
    params: React.PropTypes.object,
  };

  constructor(props) {
    super(props);

    this.state = {
      isProcessing: false,
    };
  }

  componentWillMount() {
    this.onStartRecheck();
  }

  onStartRecheck() {
    if (!this.state.isProcessing){

      this.setState({
        isProcessing: true,
        processSuccessful: false,
      });

      Relay.Store.commitUpdate(
        new ValidateStateMutation(),
        {
          onSuccess: (response) => {
            console.log('response', response);
            const responsePayload = response.klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_recheckState;

            if (responsePayload.errors) {
              this.setState({
                processSuccessful: false,
                processErrors: true,
              });
            } else {
              // console.log('result update nodes', responsePayload.result);
              // total success
              this.setState({
                isProcessing: false,
                processErrors: null,
                processSuccessful: true,
              });
              browserHistory.push(`${decodeURIComponent(this.props.location.query.from)}`);
            }
          },
          onFailure: (transaction) => {
            this.setState({
              isProcessing: false,
              processErrors: true,
            });
            console.log(transaction)},
        },
      );
    }
  }

  render () {
    return (
      <div>
        {this.state.processErrors &&
        <div>
          <h2>Error!</h2>
          <p>Server is inaccessible or has encountered an error.</p>
        </div>
        }
        {this.state.processing &&
          <div>
            <h2>Validating State</h2>
            <p>Please wait…</p>
          </div>
        }
      </div>
    )
  }
}

export default ValidateStatePage
