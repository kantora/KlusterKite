import React from 'react';
import { Pagination } from 'react-bootstrap';

export default class Paginator extends React.Component {
  static propTypes = {
    totalItems: React.PropTypes.number.isRequired,
    currentPage: React.PropTypes.number.isRequired,
    itemsPerPage: React.PropTypes.number.isRequired,
    onSelect: React.PropTypes.func.isRequired,
  };

  handleSelect(page) {
    this.props.onSelect(page);
  }

  render() {
    const totalPages = Math.ceil(this.props.totalItems / this.props.itemsPerPage);

    return (
      <div>
        {totalPages > 1 &&
        <Pagination
          prev
          next
          first
          last
          ellipsis
          boundaryLinks
          items={totalPages}
          maxButtons={5}
          activePage={this.props.currentPage}
          onSelect={this.handleSelect.bind(this)}/>
        }
      </div>
    )
  }
}