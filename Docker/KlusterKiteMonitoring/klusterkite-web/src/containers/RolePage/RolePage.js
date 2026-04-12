import React from 'react'
import Relay from 'react-relay'
import { browserHistory } from 'react-router'

import RoleForm from '../../components/RoleForm/RoleForm'

import { hasPrivilege } from '../../utils/privileges';

import CreateRoleMutation from './mutations/CreateRoleMutation'
import UpdateRoleMutation from './mutations/UpdateRoleMutation'

class RolePage extends React.Component {

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
    if (this.isAddNew()){
      this.addNode(configurationModel);
    } else {
      this.editNode(configurationModel);
    }
  };

  addNode = (model) => {
    console.log('create', model);
    Relay.Store.commitUpdate(
      new CreateRoleMutation(
        {
          name: model.name,
          allowedScope: model.allowedScope,
          deniedScope: model.deniedScope,
        }),
      {
        onSuccess: (response) => {
          console.log('response', response);
          if (response.klusterKiteNodeApi_klusterKiteNodesApi_roles_create.errors &&
            response.klusterKiteNodeApi_klusterKiteNodesApi_roles_create.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_roles_create.errors.edges);

            this.setState({
              saving: false,
              saveErrors: messages
            });
          } else {
            console.log('success', response);
            this.setState({
              saving: false,
              saveErrors: null
            });
            browserHistory.push(`/klusterkite/Roles/`);
          }
        },
        onFailure: (transaction) => console.log(transaction),
      },
    )
  };

  editNode = (model) => {
    this.setState({
      saving: true
    });

    Relay.Store.commitUpdate(
      new UpdateRoleMutation(
        {
          nodeId: this.props.params.id,
          uid: this.props.api.role.uid,
          name: model.name,
          allowedScope: model.allowedScope,
          deniedScope: model.deniedScope,
        }),
      {
        onSuccess: (response) => {
          // console.log('response', response);
          if (response.klusterKiteNodeApi_klusterKiteNodesApi_roles_update.errors &&
            response.klusterKiteNodeApi_klusterKiteNodesApi_roles_update.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_roles_update.errors.edges);

            this.setState({
              saving: false,
              saveErrors: messages
            });
          } else {
            this.setState({
              saving: false,
              saveErrors: null
            });
            browserHistory.push(`/klusterkite/Roles/`);
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

  render () {
    const model = this.props.api.role;
    const canEdit = hasPrivilege('KlusterKite.NodeManager.User.Update');

    return (
      <div>
        <RoleForm
          onSubmit={this.onSubmit}
          onDelete={this.onDelete}
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
  RolePage,
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
          role: node(id: $id) @include( if: $nodeExists ) {
            ...on IKlusterKiteNodeApi_Role {
              id
              uid
              name
              allowedScope
              deniedScope
            }
          }
        }
      `,
    },
  },
)
