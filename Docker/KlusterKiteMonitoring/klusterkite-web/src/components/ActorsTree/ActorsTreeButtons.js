import React  from 'react';

import './styles.css';

export default class ActorsTreeButtons extends React.Component {
  static propTypes = {
    handleScan: React.PropTypes.func.isRequired,
    handleReload: React.PropTypes.func.isRequired,
    isLoading: React.PropTypes.bool.isRequired,
  };

  render() {
    let reloadClassName = 'fa fa-refresh';
    if (this.props.isLoading) {
      reloadClassName += ' fa-spin';
    }

    return (
      <div className="panel row">
        <div className="col-md-6">
          <div className="alert alert-warning" role="alert">
            <div className="scanPanel">
              <button
                type="button"
                className="btn btn-warning btn-sm"
                onClick={this.props.handleScan}
                disabled={this.props.isLoading}
              >
                <i className={reloadClassName}/> {' '} Scan
              </button>
              <p className="text-margined">Scan generates additional load to all cluster node. Do this only in emergency situations or in
                development infrastructure.</p>
            </div>
          </div>
        </div>
        <div className="col-md-6">
          <div className="alert alert-info" role="alert">
            <div className="scanPanel">
              <button
                type="button"
                className="btn btn-primary btn-sm"
                onClick={this.props.handleReload}
                disabled={this.props.isLoading}
              >
                <i className={reloadClassName}/> {' '} Reload tree
              </button>
              <p className="text-margined">
                Try to reload tree before initiating a new scan, because scan can take some time and can be unfinished in time of tree load.</p>
            </div>
          </div>
        </div>
      </div>
    );
  }
}
