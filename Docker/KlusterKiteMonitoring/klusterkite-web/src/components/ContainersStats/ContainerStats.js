import React from 'react';
import Relay from 'react-relay'

import './styles.css';

export class ContainerStats extends React.Component {
  static propTypes = {
    klusterKiteNodesApi: React.PropTypes.object,
  };

  render() {
    const containers = this.props.klusterKiteNodesApi.getActiveNodeDescriptions.edges.map(x => x.node.containerType);
    let counts = {};
    containers.forEach((x) => { if (x !== null) { counts[x] = (counts[x] || 0)+1 }});
    console.log('containers', containers);
    console.log('counts', counts);

    return (
      <div>
        <h3>Containers</h3>
        <div className="templates">
          {Object.keys(counts).map((key) =>
            <div key={key}>
              <span className="label label-success">
                {key}: {counts[key]}
              </span>
            </div>
          )}
        </div>
      </div>
    );
  }
}

export default Relay.createContainer(
  ContainerStats,
  {
    fragments: {
      klusterKiteNodesApi: () => Relay.QL`fragment on IKlusterKiteNodeApi_Root {
        getActiveNodeDescriptions {
          edges {
            node {
              containerType
            }
          }
        }
      }
      `,
    },
  },
)
