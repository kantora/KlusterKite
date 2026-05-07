import React from 'react';
import Icon from 'react-fa';

import './styles.css';

export default class SortableHeader extends React.Component {

  static propTypes = {
    title: React.PropTypes.string.isRequired,
    code: React.PropTypes.string.isRequired,
    sortColumn: React.PropTypes.string.isRequired,
    sortDirection: React.PropTypes.string.isRequired,
    onSort: React.PropTypes.func.isRequired,
  };

  render() {
    const newDirection = this.props.sortColumn === this.props.code ? (this.props.sortDirection === 'desc' ? 'asc' : 'desc') : 'asc';

    return (
      <th onClick={() => {this.props.onSort(this.props.code, newDirection)}} className="sorting_td">
        <nobr>
          {this.props.title}
          {this.props.sortColumn === this.props.code && this.props.sortDirection === 'desc' && <Icon name="sort-asc" className="sorting_icon sorting_icon_asc" />}
          {this.props.sortColumn === this.props.code && this.props.sortDirection === 'asc' && <Icon name="sort-desc" className="sorting_icon sorting_icon_desc" />}
        </nobr>
      </th>
    );
  }
}
