import React from 'react'
import Relay from 'react-relay'
import { browserHistory } from 'react-router'

import ResetPassword from '../../components/UserEdit/ResetPassword'

import { hasPrivilege } from '../../utils/privileges';

import ResetPasswordMutation from './mutations/ResetPasswordMutation'

class ResetPasswordPage extends React.Component {

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
      saving: false,
      saveErrors: null,
    }
  }

  getErrorMessagesFromEdge = (edges) => {
    return edges.map(x => x.node).map(x => x.message);
  }

  onResetPassword(model, userUid) {
    console.log('reset password', model, userUid);

    this.setState({
      saving: true
    });

    Relay.Store.commitUpdate(
      new ResetPasswordMutation(
        {
          password: model.password,
          userUid: userUid,
        }),
      {
        onSuccess: (response) => {
          console.log('response', response);
          if (response.klusterKiteNodeApi_klusterKiteNodesApi_users_resetPassword.errors &&
            response.klusterKiteNodeApi_klusterKiteNodesApi_users_resetPassword.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_users_resetPassword.errors.edges);

            this.setState({
              saving: false,
              saveErrors: messages
            });
          } else {
            this.setState({
              saving: false,
              saveErrors: null
            });

            browserHistory.push(`/klusterkite/Users/`);
          }
        },
        onFailure: (transaction) => {
          this.setState({
            saving: false,
            saveErrors: ['Bad request']
          });
          console.log(transaction)},
      },
    )
  }

  render () {
    const model = this.props.api.user;
    const canEdit = hasPrivilege('KlusterKite.NodeManager.User.Update');

    return (
      <div>
        <ResetPassword
          onSubmit={this.onResetPassword.bind(this)}
          initialValues={model}
          saving={this.state.saving}
          saveErrors={this.state.saveErrors}
          canEdit={canEdit}
        />
      </div>
    )
  }
}

export default Relay.createContainer(
  ResetPasswordPage,
  {
    initialVariables: {
      id: null,
      nodeExists: false,
    },
    prepareVariables: (prevVariables) => Object.assign({}, prevVariables, {
      nodeExists: prevVariables.id !== null,
    }),
    fragments: {
      api: () => Relay.QL`
        fragment on IKlusterKiteNodeApi {
          id
          user: node(id: $id) @include( if: $nodeExists ) {
            ...on IKlusterKiteNodeApi_User {
              id
              uid
              login
            }
          }
        }
      `,
    },
  },
)
