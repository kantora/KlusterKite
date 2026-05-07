import React from 'react';
import Relay from 'react-relay'

import { Link } from 'react-router';

class SeedsList extends React.Component {
  constructor (props) {
    super(props);
    this.state = {
    }
  }

  static propTypes = {
    configurationId: React.PropTypes.string,
    configuration: React.PropTypes.object,
    canEdit: React.PropTypes.bool
  };

  render() {
    const seeds = this.props.settings && this.props.settings.seedAddresses;

    return (
      <div>
        <div>
          <h3>Akka Cluster Seeds</h3>
          {this.props.canEdit &&
            <Link to={`/klusterkite/Seeds/${this.props.configurationId}`} className="btn btn-primary" role="button">Add a new
              seed</Link>
          }
          {seeds && seeds.length > 0 &&
          <table className="table table-hover">
            <thead>
            <tr>
              <th>Address</th>
            </tr>
            </thead>
            <tbody>
            {seeds.map((item, index) =>
              <tr key={index}>
                <td>
                  {this.props.canEdit &&
                    <Link to={`/klusterkite/Seeds/${this.props.configurationId}`}>
                      {item}
                    </Link>
                  }
                  {!this.props.canEdit &&
                    <span>{item}</span>
                  }
                </td>
              </tr>
            )
            }
            </tbody>
          </table>
          }
        </div>
      </div>
    );
  }
}

export default Relay.createContainer(
  SeedsList,
  {
    fragments: {
      settings: () => Relay.QL`fragment on IKlusterKiteNodeApi_ConfigurationSettings {
        seedAddresses
      }
      `,
    },
  },
)
