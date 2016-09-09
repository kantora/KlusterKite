import React, { Component, PropTypes } from 'react';

export default class Hidden extends Component {
  static propTypes = {
    input: PropTypes.object.isRequired,
    name: PropTypes.string.isRequired
  }

  render() {
    const {input, name} = this.props;

    return (
      <input type="hidden" id={name} {...input}/>
    );
  }
}