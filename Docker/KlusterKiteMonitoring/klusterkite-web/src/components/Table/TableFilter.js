import React from 'react';
import Icon from 'react-fa';
import debounce from 'lodash/debounce'

import './styles.css';

export default class TableFilter extends React.Component {
  constructor(props) {
    super(props);

    this.applyFilter = debounce(this.applyFilter, 300);

    this.state = {
      filter: '',
    };
  }

  static propTypes = {
    onFilter: React.PropTypes.func.isRequired,
  };

  setFilter(event) {
    this.setState({filter: event.target.value});
    this.applyFilter(event.target.value);
  }

  /**
   * Applies filter after debounce delay (defined in the constructor)
   * @param filter {string} Filter text
   */
  applyFilter(filter) {
    this.props.onFilter(filter);
  }

  render() {
    return (
      <div>
        <div className="table-filter">
          {this.props.children}
          <div className="table-filter-filter">
            <div className="input-group">
              <span className="input-group-addon"><Icon name="search" /></span>
              <input type="text" className="form-control" value={this.state.filter} onChange={this.setFilter.bind(this)} placeholder="Filter" />
            </div>
          </div>
        </div>
        <div className="table-filter-clear" />
      </div>
    );
  }
}
