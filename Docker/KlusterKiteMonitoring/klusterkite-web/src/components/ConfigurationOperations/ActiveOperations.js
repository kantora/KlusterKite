import React from 'react';
import Relay from 'react-relay'
import Icon from 'react-fa';
import delay from 'lodash/delay'

import SetStableMutation from './mutations/SetStableMutation';

import './styles.css';

export default class ActiveOperations extends React.Component {
  constructor(props) {
    super(props);

    this.state = {
      isSettingStable: false,
      setStableSuccessful: false,
      isChangingState: false,
    };
  }

  static propTypes = {
    configurationId: React.PropTypes.string.isRequired,
    configurationInnerId: React.PropTypes.number.isRequired,
    currentState: React.PropTypes.string.isRequired,
    isStable: React.PropTypes.bool.isRequired,
    onForceFetch: React.PropTypes.func.isRequired,
  };

  onSetStable = () => {
    if (!this.state.isSettingStable){
      console.log('set stable');

      this.setState({
        isSettingStable: true,
        setStableSuccessful: false
      });

      Relay.Store.commitUpdate(
        new SetStableMutation(
          {
            configurationId: this.props.configurationInnerId,
          }),
        {
          onSuccess: (response) => {
            console.log('response', response);
            if (response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_setStable.errors &&
              response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_setStable.errors.edges) {
              const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_setStable.errors.edges);

              this.setState({
                isSettingStable: false,
                setStableErrors: messages
              });
            } else {
              console.log('result set stable', response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_setStable);
              // total success
              this.setState({
                isSettingStable: false,
                setStableErrors: null,
                setStableSuccessful: true,
              });

              this.refetchAfterDelay(1000);
            }
          },
          onFailure: (transaction) => {
            this.setState({
              isSettingStable: false
            });
            console.log(transaction)},
        },
      );
    }
  };

  /**
   * Refetches data from GraphQL server after a delay
   * @param delayTime {number} Delay in ms
   */
  refetchAfterDelay = (delayTime) => {
    delay(() => {
      this.props.onForceFetch();
    }, delayTime);

    delay(() => {
      this.setState({
        isChangingState: false
      });
    }, delayTime + 1000);
  };

  getErrorMessagesFromEdge = (edges) => {
    return edges.map(x => x.node).map(x => x.message);
  };

  render() {
    let setStableClassName = '';
    if (this.state.isSettingStable) {
      setStableClassName += ' fa-spin';
    }

    return (
    <div>
      {this.state.setStableErrors && this.state.setStableErrors.map((error, index) => {
        return (
          <div className="alert alert-danger" role="alert" key={`error-${index}`}>
            <span className="glyphicon glyphicon-exclamation-sign" aria-hidden="true"></span>
            {' '}
            {error}
          </div>
        );
      })
      }

      {this.state.setStableSuccessful && !this.state.isChangingState &&
      <div>
        <div className="alert alert-success" role="alert">
          <span className="glyphicon glyphicon-ok" aria-hidden="true"></span>
          {' '}
          Set stable successful!
        </div>
      </div>
      }

      {false && this.props.currentState && this.props.currentState === 'Active' && !this.props.isStable && !this.state.isChangingState &&
      <button className="btn btn-default" type="button" onClick={this.onSetStable}>
        <Icon name="thumbs-up" className={setStableClassName}/>{' '}Set stable
      </button>
      }

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
