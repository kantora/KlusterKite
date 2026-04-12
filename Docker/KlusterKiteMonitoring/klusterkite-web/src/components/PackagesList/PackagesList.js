import React from 'react';
import Relay from 'react-relay'

import { Link } from 'react-router';

import './styles.css';

export class PackagesList extends React.Component { // eslint-disable-line react/prefer-stateless-function
  constructor(props) {
    super(props);

    this.state = {
      showUpdated: true,
    };
  }

  static propTypes = {
    configurationId: React.PropTypes.string,
    configuration: React.PropTypes.object.isRequired,
    activeConfigurationPackages: React.PropTypes.object,
    canEdit: React.PropTypes.bool,
  };

  onUpdatedChange() {
    this.setState((prevState) => ({
      showUpdated: !prevState.showUpdated
    }));
  }

  render() {
    const packages = this.props.configuration && this.props.configuration.packages && this.props.configuration.packages.edges;
    const activeConfigurationPackages = this.props.activeConfigurationPackages.edges;

    let packagesFiltered = [];
    packages.forEach((item) => {
      const isOld = activeConfigurationPackages.some(element => (element.node._id === item.node._id && element.node.version === item.node.version));
      if (!isOld || !this.state.showUpdated) {
        packagesFiltered.push({
          id: item.node.id,
          name: item.node._id,
          version: item.node.version,
          isNew: !isOld
        });
      }
    });

    return (
      <div>
        <div>
          <h3>Packages list</h3>
          {this.props.canEdit &&
            <Link to={`/klusterkite/Packages/${this.props.configurationId}`} className="btn btn-primary" role="button">Add/edit packages</Link>
          }

          <p>
            <label className="checkbox-label"><input type="checkbox" checked={this.state.showUpdated} onChange={this.onUpdatedChange.bind(this)} /> Show changed only</label>
          </p>
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
            {packagesFiltered.map((item) =>
              <tr key={item.id}>
                <td>
                  <Link to={`/klusterkite/Packages/${this.props.configurationId}`}>
                    {item.name}
                  </Link>
                </td>
                <td>
                  {item.version}
                </td>
                <td>
                  {item.isNew.toString()}
                </td>
              </tr>
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
    fragments: {
      configuration: () => Relay.QL`fragment on IKlusterKiteNodeApi_ConfigurationSettings {
        packages(sort: id_asc) {
          edges {
            node {
              version
              id
              _id
            }
          }
        }
      }
      `,
    },
  },
)

