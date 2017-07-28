import React from 'react'
import Relay from 'react-relay'
import Icon from 'react-fa';
import isEqual from 'lodash/isEqual';
import { Link } from 'react-router'

import UpdateFeedMutation from '../../containers/FeedPage/mutations/UpdateFeedMutation'
import TableFilter from '../Table/TableFilter'

import './styles.css';

export class PackagesList extends React.Component { // eslint-disable-line react/prefer-stateless-function
  constructor(props) {
    super(props);
    this.onChangeVersion = this.onChangeVersion.bind(this);
    this.onStartEdit = this.onStartEdit.bind(this);

    this.state = {
      showUpdated: true,
      filter: '',
      nugetPackagesList: null,
      editableIds: [],
      packagesCache: null,
      changedVersions: [],
    };
  }

  static propTypes = {
    configurationId: React.PropTypes.string,
    configurationInnerId: React.PropTypes.number,
    settings: React.PropTypes.object.isRequired,
    activeConfigurationPackages: React.PropTypes.object,
    canEdit: React.PropTypes.bool,
    nugetPackagesList: React.PropTypes.object,
    currentState: React.PropTypes.string.isRequired,
  };

  componentWillMount() {
    this.onReceiveProps(this.props, true);
  }

  componentWillReceiveProps(nextProps) {
    this.onReceiveProps(nextProps, false);
  }

  onReceiveProps(nextProps, skipCheck) {
    if (nextProps.nugetPackagesList && (!isEqual(nextProps.nugetPackagesList, this.props.nugetPackagesList) || skipCheck)) {
      const packagesNodes = nextProps.nugetPackagesList.edges.map(x => x.node);
      this.setState({
        nugetPackagesList: packagesNodes
      })
    }

    // We save unfiltered packages list to use later in migration
    if (nextProps.settings && nextProps.settings.packages && !this.state.packagesCache) {
      this.setState({
        packagesCache: nextProps.settings.packages
      })
    }

    if (nextProps.currentState !== 'Draft') {
      this.setState({
        showUpdated: false
      });
    }
  }

  onUpdatedChange() {
    this.setState((prevState) => ({
      showUpdated: !prevState.showUpdated
    }));
  }

  /**
   * Applies filter to Relay variables after debounce (defined in the constructor)
   * @param filter {string} Filter text
   */
  applyFilter(filter) {
    this.props.relay.setVariables({
      filter: { id_l_contains: filter.toLowerCase() }
    });
    this.setState({
      filter: filter
    });
  }

  onChangeVersion(item, newValue) {
    const packages = this.state.packagesCache.edges.map(x => {
      return {
        id: x.node.__id,
        version: x.node.id === item.id ? newValue : x.node.version
      }
    });

    this.savePackages(packages);
  }

  onStartEdit(id) {
    this.setState((prevState) => ({
      editableIds: [
        ...prevState.editableIds,
        id,
      ],
    }));
  }

  savePackages = (packages) => {
    this.setState({
      saving: true
    });

    Relay.Store.commitUpdate(
      new UpdateFeedMutation(
        {
          nodeId: this.props.configurationId,
          configurationId: this.props.configurationInnerId,
          settings: this.props.settings,
          packagesList: packages,
        }),
      {
        onSuccess: (response) => {
          if (response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors &&
            response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors.edges);

            this.setState({
              saving: false,
              saveErrors: messages
            });
          } else {
            // ok
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

  render() {
    const packages = this.props.settings && this.props.settings.packages && this.props.settings.packages.edges;
    const activeConfigurationPackages = this.props.activeConfigurationPackages.edges;

    let packagesFiltered = [];
    packages.forEach((item) => {
      const isOld = activeConfigurationPackages.some(element => (element.node.__id === item.node.__id && element.node.version === item.node.version));
      if (!isOld || !this.state.showUpdated) {
        packagesFiltered.push({
          id: item.node.id,
          name: item.node.__id,
          version: item.node.version,
          isNew: !isOld
        });
      }
    });

    return (
      <div>
        <div>
          <h3>Packages</h3>
          {this.props.canEdit &&
            <div className="buttons-block-margin">
              <Link to={`/klusterkite/Packages/${this.props.configurationId}`} className="btn btn-primary" role="button">Add/edit packages</Link>

              {this.props.currentState && this.props.currentState === 'Draft' &&
                <Link to={`/klusterkite/CopyConfig/${this.props.configurationId}/updateCurrent`} className="btn btn-success btn-margined" role="button">
                  <Icon name="clone"/>{' '}Update all packages to the latest version
                </Link>
              }
            </div>
          }

          <TableFilter onFilter={this.applyFilter.bind(this)}>
            {this.props.currentState === 'Draft' &&
              <p className="table-filter-element">
                <label className="checkbox-label"><input type="checkbox" checked={this.state.showUpdated} onChange={this.onUpdatedChange.bind(this)} /> Show changed only</label>
              </p>
            }
          </TableFilter>

          <div className="table-filter-clear"></div>
          {packagesFiltered && packagesFiltered.length === 0 && this.state.filter.length === 0 &&
            <p>No changed packages.</p>
          }
          {packagesFiltered && packagesFiltered.length === 0 && this.state.filter.length > 0 &&
            <p>No packages found.</p>
          }
          {packagesFiltered && packagesFiltered.length > 0 &&
          <table className="table table-hover">
            <thead>
            <tr>
              <th>Id</th>
              <th>Version</th>
              <th>Changed</th>
            </tr>
            </thead>
            <tbody>
            {packagesFiltered.map((item) => {
              const nugetFeedNode = this.state.nugetPackagesList.find(x => x.name === item.name);
              const isEditable = this.state.editableIds.includes(item.id);
              return (
                <tr key={item.id}>
                  <td>
                    <Link to={`/klusterkite/Packages/${this.props.configurationId}`}>
                      {item.name}
                    </Link>
                  </td>
                  <td>
                    {this.props.canEdit && !isEditable &&
                      <span className="pseudohref" onClick={() => {this.onStartEdit(item.id)}}>{item.version}</span>
                    }
                    {this.props.canEdit && isEditable &&
                      <select defaultValue={item.version} onChange={(event) => { this.onChangeVersion(item, event.target.value) }}>
                        {nugetFeedNode.availableVersions.map((version) => <option key={version} value={version}>{version}</option>)}
                      </select>
                    }
                    {!this.props.canEdit &&
                      <span>{item.version}</span>
                    }
                  </td>
                  <td>
                    {item.isNew.toString()}
                  </td>
                </tr>
              )}
            )}
            </tbody>
          </table>
          }
        </div>
      </div>
    );
  }
}

export default Relay.createContainer(
  PackagesList,
  {
    initialVariables: {
      filter: { id_l_contains: '' },
    },
    fragments: {
      settings: () => Relay.QL`fragment on IKlusterKiteNodeApi_ConfigurationSettings {
        packages(sort: id_asc, filter: $filter ) {
          edges {
            node {
              version
              id
              __id
            }
          }
        },
        ${UpdateFeedMutation.getFragment('settings')},
      }
      `,
    },
  },
)

