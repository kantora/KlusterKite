import React from 'react'
import Relay from 'react-relay'
import { browserHistory } from 'react-router'

import ConfigurationOperations from '../../components/ConfigurationOperations/ConfigurationOperations';
import ConfigurationForm from '../../components/ConfigurationForm/ConfigurationForm'
import FeedsList from '../../components/FeedsList/FeedList'
import MigratorTemplatesList from '../../components/MigratorTemplatesList/MigratorTemplatesList'
import NodeTemplatesList from '../../components/NodeTemplatesList/NodeTemplatesList'
import PackagesList from '../../components/PackagesList/PackagesList'
import SeedsList from '../../components/SeedsList/SeedsList'
import Warnings from '../../components/Warnings/Warnings';

import CreateConfigurationMutation from './mutations/CreateConfigurationMutation'
import UpdateConfigurationMutation from './mutations/UpdateConfigurationMutation'

class ConfigurationPage extends React.Component {

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

  onStartMigration = () => {
    browserHistory.push(`/klusterkite/Migration/`);
  };

  addNode = (model) => {
    Relay.Store.commitUpdate(
      new CreateConfigurationMutation(
        {
          majorVersion: model.majorVersion,
          minorVersion: model.minorVersion,
          name: model.name,
          notes: model.notes,
        }),
      {
        onSuccess: (response) => {
          if (response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_create.errors &&
            response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_create.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_create.errors.edges);

            this.setState({
              saving: false,
              saveErrors: messages
            });
          } else {
            this.setState({
              saving: false,
              saveErrors: null
            });
            if (this.props.params.mode === 'update') {
              browserHistory.push(`/klusterkite/CopyConfig/${response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_create.node.id}/update`);
            } else {
              browserHistory.push(`/klusterkite/CopyConfig/${response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_create.node.id}/exact`);
            }
          }
        },
        onFailure: (transaction) => console.log(transaction),
      },
    )
  };

  editNode = (model) => {
    console.log('saving', model);

    this.setState({
      saving: true
    });

    Relay.Store.commitUpdate(
      new UpdateConfigurationMutation(
        {
          nodeId: this.props.params.id,
          __id: model.__id,
          majorVersion: model.majorVersion,
          minorVersion: model.minorVersion,
          name: model.name,
          notes: model.notes
        }),
      {
        onSuccess: (response) => {
          console.log('response', response);
          if (response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors &&
            response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors.edges);

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
            saving: false
          });
          console.log(transaction)},
      },
    )
  };

  getErrorMessagesFromEdge = (edges) => {
    return edges.map(x => x.node).map(x => x.message);
  }

  onDelete = () => {
    console.log('delete', this.props.api.__node.__id);
    // Relay.Store.commitUpdate(
    //   new DeleteFeedMutation({deletedId: this.props.api.__node.__id}),
    //   {
    //     onSuccess: () => this.context.router.replace('/klusterkite/NugetFeeds'),
    //     onFailure: (transaction) => console.log(transaction),
    //   },
    // )
  }

  render () {
    const model = this.props.api.configuration;
    const activeConfiguration = this.props.api.activeConfiguration && this.props.api.activeConfiguration.configurations && this.props.api.activeConfiguration.configurations.edges && this.props.api.activeConfiguration.configurations.edges[0].node;
    const klusterKiteNodesApi = this.props.api.klusterKiteNodesApi;
    const nodeManagement = klusterKiteNodesApi.clusterManagement;
    const packages = this.props.api.klusterKiteNodesApi.nugetPackages;

    const canEdit = !model || model.state === 'Draft';
    return (
      <div>
        <Warnings
          klusterKiteNodesApi={this.props.api.klusterKiteNodesApi}
          migrationWarning={true}
          notInSourcePositionWarning={true}
          migratableResourcesWarning={true}
          outOfScopeWarning={true}
        />
        <ConfigurationForm
          onSubmit={this.onSubmit}
//          onDelete={this.onDelete}
          initialValues={model}
          saving={this.state.saving}
          saveErrors={this.state.saveErrors}
          canEdit={canEdit}
          activeConfiguration={activeConfiguration}
        />
        {model &&
        <div>
          <ConfigurationOperations
            settings={model.settings}
            nodeManagement={nodeManagement}
            klusterKiteNodesApi={klusterKiteNodesApi}
            configurationId={this.props.params.id}
            configurationInnerId={model.__id}
            currentState={model.state}
            onForceFetch={this.props.relay.forceFetch}
            isStable={model.isStable}
            onStartMigration={this.onStartMigration.bind(this)}
          />
          <FeedsList
            settings={model.settings}
            configurationId={this.props.params.id}
            configurationInnerId={model.__id}
            canEdit={canEdit}
          />
          <NodeTemplatesList
            settings={model.settings}
            createNodeTemplatePrivilege={true}
            getNodeTemplatePrivilege={true}
            configurationId={this.props.params.id}
            canEdit={canEdit}
          />
          <MigratorTemplatesList
            settings={model.settings}
            createMigratorTemplatePrivilege={true}
            getMigratorTemplatePrivilege={true}
            configurationId={this.props.params.id}
            canEdit={canEdit}
          />
          <SeedsList
            settings={model.settings}
            configurationId={this.props.params.id}
            canEdit={canEdit}
          />
          <PackagesList
            settings={model.settings}
            configurationId={this.props.params.id}
            configurationInnerId={model.__id}
            activeConfigurationPackages={activeConfiguration.settings.packages}
            canEdit={canEdit}
            nugetPackagesList={packages}
            currentState={model.state}
          />
        </div>
        }
      </div>
    )
  }
}

export default Relay.createContainer(
  ConfigurationPage,
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
          configuration: __node(id: $id) @include( if: $nodeExists ) {
            ...on IKlusterKiteNodeApi_Configuration {
              __id
              name
              notes
              minorVersion
              majorVersion
              state
              isStable
              created
              finished
              settings {
                ${FeedsList.getFragment('settings')},
                ${MigratorTemplatesList.getFragment('settings')}
                ${NodeTemplatesList.getFragment('settings')}
                ${PackagesList.getFragment('settings')}
                ${SeedsList.getFragment('settings')}
                ${ConfigurationOperations.getFragment('settings')}
                id
              }
            }
          },
          activeConfiguration: klusterKiteNodesApi {
            configurations (filter: {state: Active}, limit: 1) {
              edges {
                node {
                  minorVersion
                  majorVersion
                  settings {
                    packages {
                      edges {
                        node {
                          __id
                          version
                        }
                      }
                    }
                  }
                }
              }
            }
          },
          klusterKiteNodesApi {
            clusterManagement {
              ${ConfigurationOperations.getFragment('nodeManagement')}
            }
            ${ConfigurationOperations.getFragment('klusterKiteNodesApi')}
            ${Warnings.getFragment('klusterKiteNodesApi')}
            nugetPackages {
              edges {
                node {
                  name
                  version
                  availableVersions
                }
              }
            }
          }
        }
      `,
    },
  },
)
