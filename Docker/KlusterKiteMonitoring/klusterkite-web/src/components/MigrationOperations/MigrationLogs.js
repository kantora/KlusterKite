import React from 'react';
import Relay from 'react-relay'

import DateFormat from '../../utils/date'

import './styles.css';

export class MigrationLogs extends React.Component {
  static propTypes = {
    currentMigration: React.PropTypes.object,
  };

  render() {
    return (
      <div>
        {this.props.currentMigration.logs && this.props.currentMigration.logs.edges.length > 0 &&
          <div className="migration-logs">
            <h3>Logs</h3>
            <table className="table table-hover">
              <thead>
              <tr>
                <th>Started</th>
                <th>Finished</th>
                <th>Template</th>
                <th>Resource</th>
                <th>Message</th>
              </tr>
              </thead>
              <tbody>
              {this.props.currentMigration.logs.edges.map((edge, index) => {
                const node = edge.node;
                return (
                  <tr key={`migration-log-${index}`}>
                    <td>{node.started && DateFormat.formatTime(new Date(node.started))}</td>
                    <td>
                      {node.finished && DateFormat.formatTime(new Date(node.finished))}
                      {!node.finished && node.started && DateFormat.formatTime(new Date(node.started))}
                    </td>
                    <td>{node.migratorTemplateName}</td>
                    <td>{node.resourceName}</td>
                    <td>{node.message}</td>
                  </tr>
                );
              })}
              </tbody>
            </table>
          </div>
        }
      </div>
    );
  }
}

export default Relay.createContainer(
  MigrationLogs,
  {
    fragments: {
      currentMigration: () => Relay.QL`fragment on IKlusterKiteNodeApi_Migration {
        logs {
          edges {
            node {
              started
              finished
              type
              sourcePoint
              destinationPoint
              message
              errorStackTrace
              migratorTemplateCode
              migratorTemplateName
              migratorTypeName
              migratorName
              resourceCode
              resourceName
            }
          }
        }
      }
      `,
    },
  },
)
