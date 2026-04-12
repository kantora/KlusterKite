import React from 'react'
import Relay from 'react-relay'
import { browserHistory } from 'react-router'

import UserForm from '../../components/UserEdit/UserForm'
import ResetPassword from '../../components/UserEdit/ResetPassword'
import RolesAccess from '../../components/UserEdit/RolesAccess'

import { hasPrivilege } from '../../utils/privileges';

import CreateUserMutation from './mutations/CreateUserMutation'
import UpdateUserMutation from './mutations/UpdateUserMutation'
import UserGrantRoleMutation from './mutations/UserGrantRoleMutation'
import UserWithdrawRoleMutation from './mutations/UserWithdrawRoleMutation'
import ResetPasswordMutation from './mutations/ResetPasswordMutation'

class UserPage extends React.Component {

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

  isAddNew = () => {
    return !this.props.params.hasOwnProperty('id')
  };

  onSubmit = (configurationModel) => {
    console.log('submitting configuration', configurationModel);
    console.log('current model', this.props.api.configuration);

    if (this.isAddNew()){
      this.addNode(configurationModel);
    } else {
      this.editNode(configurationModel);
    }
  };

  addNode = (model) => {
    console.log('create', model);
    this.setState({
      saving: true
    });

    Relay.Store.commitUpdate(
      new CreateUserMutation(
        {
          login: model.login,
          isBlocked: model.isBlocked,
          isDeleted: model.isDeleted,
        }),
      {
        onSuccess: (response) => {
          const data = response.klusterKiteNodeApi_klusterKiteNodesApi_users_create;

          if (data.errors &&
            data.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(data.errors.edges);

            this.setState({
              saving: false,
              saveErrors: messages
            });
          } else {
            this.setState({
              saving: false,
              saveErrors: null
            });
            browserHistory.push(`/klusterkite/Users/${data.node.id}`);
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
  };

  editNode = (model) => {
    console.log('saving', model);

    this.setState({
      saving: true
    });

    Relay.Store.commitUpdate(
      new UpdateUserMutation(
        {
          nodeId: this.props.params.id,
          uid: model.uid,
          login: model.login,
          isBlocked: model.isBlocked,
          isDeleted: model.isDeleted,
        }),
      {
        onSuccess: (response) => {
          console.log('response', response);
          const data = response.klusterKiteNodeApi_klusterKiteNodesApi_users_update;

          if (data.errors &&
            data.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(data.errors.edges);

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
  };

  getErrorMessagesFromEdge = (edges) => {
    return edges.map(x => x.node).map(x => x.message);
  }

  onDelete = () => {
    console.log('delete', this.props.api.node._id);
    // Relay.Store.commitUpdate(
    //   new DeleteFeedMutation({deletedId: this.props.api.node._id}),
    //   {
    //     onSuccess: () => this.context.router.replace('/klusterkite/NugetFeeds'),
    //     onFailure: (transaction) => console.log(transaction),
    //   },
    // )
  }

  onResetPassword(model, userUid) {
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

  /**
   * Grants of revokes role
   * @param roleUid {string} Role uid
   * @param userUid {string} User uid
   * @param hasRole {boolean} Does user have this role at this moment?
   */
  onGrantOrRevokeRole(roleUid, userUid, hasRole) {
    console.log('roleUid, userUid', roleUid, userUid);
    if (!hasRole) {
      this.grantRole(roleUid, userUid);
    } else {
      this.withdrawRole(roleUid, userUid);
    }
  }

  /**
   * Grants role
   * @param roleUid {string} Role uid
   * @param userUid {string} User uid
   */
  grantRole(roleUid, userUid) {
    this.setState({
      saving: true
    });

    Relay.Store.commitUpdate(
      new UserGrantRoleMutation(
        {
          roleUid: roleUid,
          userUid: userUid,
        }),
      {
        onSuccess: (response) => {
          console.log('response', response);
          if (response.klusterKiteNodeApi_klusterKiteNodesApi_users_grantRole.errors &&
            response.klusterKiteNodeApi_klusterKiteNodesApi_users_grantRole.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_users_grantRole.errors.edges);

            this.setState({
              saving: false,
              saveErrors: messages
            });
          } else {
            this.setState({
              saving: false,
              saveErrors: null
            });

            this.props.relay.forceFetch();
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

  /**
   * Revokes role
   * @param roleUid {string} Role uid
   * @param userUid {string} User uid
   */
  withdrawRole(roleUid, userUid) {
    this.setState({
      saving: true
    });

    Relay.Store.commitUpdate(
      new UserWithdrawRoleMutation(
        {
          roleUid: roleUid,
          userUid: userUid,
        }),
      {
        onSuccess: (response) => {
          console.log('response', response);
          if (response.klusterKiteNodeApi_klusterKiteNodesApi_users_withdrawRole.errors &&
            response.klusterKiteNodeApi_klusterKiteNodesApi_users_withdrawRole.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_users_withdrawRole.errors.edges);

            this.setState({
              saving: false,
              saveErrors: messages
            });
          } else {
            this.setState({
              saving: false,
              saveErrors: null
            });

            this.props.relay.forceFetch();
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
    const roles = this.props.api.klusterKiteNodesApi.roles && this.props.api.klusterKiteNodesApi.roles.edges.map(x => x.node);

    return (
      <div>
        <UserForm
          onSubmit={this.onSubmit}
          onDelete={this.onDelete}
          initialValues={model}
          saving={this.state.saving}
          saveErrors={this.state.saveErrors}
          canEdit={canEdit}
        />
        {model &&
          <ResetPassword
            onSubmit={this.onResetPassword.bind(this)}
            initialValues={model}
            saving={this.state.saving}
            saveErrors={this.state.saveErrors}
            canEdit={canEdit}
          />
        }
        {model &&
          <RolesAccess
            initialValues={model}
            roles={roles}
            onGrantOrRevokeRole={this.onGrantOrRevokeRole.bind(this)}
          />
        }
      </div>
    )
  }
}

export default Relay.createContainer(
  UserPage,
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
              isBlocked
              isDeleted
              roles {
                edges {
                  node {
                    role {
                      name
                    }
                  }
                }
              }
            }
          }
          klusterKiteNodesApi {
            roles {
              edges {
                node {
                  id
                  uid
                  name
                }
              }
            }
          }
        }
      `,
    },
  },
)
