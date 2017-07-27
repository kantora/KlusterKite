import React from 'react'
import Relay from 'react-relay'
import { browserHistory } from 'react-router'

import delay from 'lodash/delay'

import ContainerStats from '../../components/ContainersStats/ContainerStats';
import NodesList from '../../components/NodesList/NodesList';
import NodesWithTemplates from '../../components/NodesWithTemplates/NodesWithTemplates';
import Warnings from '../../components/Warnings/Warnings';

import { hasPrivilege } from '../../utils/privileges';

class HomePage extends React.Component {
  static propTypes = {
    api: React.PropTypes.object,
    sort: React.PropTypes.string,
  };

  componentDidMount = () => {
    delay(() => this.refetchDataOnTimer(), 10000);
  };

  componentWillUnmount = () => {
    clearTimeout(this._refreshId);
  };

  refetchDataOnTimer = () => {
    this.props.relay.forceFetch();
    this._refreshId = delay(() => this.refetchDataOnTimer(), 10000);
  };

  onSort(sort) {
    browserHistory.push(`/klusterkite/Home/${sort}`);
  }

  render () {
    return (
      <div>
        <h1>Monitoring</h1>

        <Warnings
          klusterKiteNodesApi={this.props.api.klusterKiteNodesApi}
          migrationWarning={true}
          notInSourcePositionWarning={true}
          migratableResourcesWarning={true}
          outOfScopeWarning={true}
        />
        {hasPrivilege('KlusterKite.NodeManager.GetTemplateStatistics') && this.props.api.klusterKiteNodesApi &&
          <NodesWithTemplates data={this.props.api.klusterKiteNodesApi}/>
        }
        {hasPrivilege('KlusterKite.NodeManager.GetActiveNodeDescriptions') && this.props.api.klusterKiteNodesApi &&
          <NodesList
            hideModules={true}
            hasError={false}
            upgradeNodePrivilege={hasPrivilege('KlusterKite.NodeManager.UpgradeNode')}
            nodeDescriptions={this.props.api.klusterKiteNodesApi}
            sort={this.props.sort || 'nodeTemplate_asc'}
            onSort={this.onSort}
          />
        }
        <ContainerStats
          klusterKiteNodesApi={this.props.api.klusterKiteNodesApi}
        />
      </div>
    )
  }
}

export default Relay.createContainer(
  HomePage,
  {
    initialVariables: {
      sort: 'nodeTemplate_asc',
    },
    fragments: {
      api: (variables) => Relay.QL`fragment on IKlusterKiteNodeApi {
        __typename
        klusterKiteNodesApi {
          id
          ${NodesWithTemplates.getFragment('data')},
          ${NodesList.getFragment('nodeDescriptions', { sort: variables.sort })},
          ${Warnings.getFragment('klusterKiteNodesApi')},
          ${ContainerStats.getFragment('klusterKiteNodesApi')},
        }
      }
      `,
    }
  },
)
