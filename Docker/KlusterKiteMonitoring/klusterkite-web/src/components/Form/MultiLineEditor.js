import React from 'react';
import Icon from 'react-fa';
import isEqual from 'lodash/isEqual';

import './styles.css';

export default class MultiLineEditor extends React.Component {
  static propTypes = {
    id: React.PropTypes.string.isRequired,
    label: React.PropTypes.string.isRequired,
    values: React.PropTypes.arrayOf(React.PropTypes.string).isRequired,
    onChange: React.PropTypes.func.isRequired,
  };

  constructor() {
    super();

    this.onDelete = this.onDelete.bind(this);
    this.onAdd = this.onAdd.bind(this);
  }

  componentWillMount() {
    this.onReceiveProps(this.props, true);
  }

  componentWillReceiveProps(nextProps) {
    this.onReceiveProps(nextProps, false);
  }

  onReceiveProps(nextProps, skipCheck) {
    if (nextProps.values && (!isEqual(nextProps.values, this.props.values) || skipCheck)) {
      this.setState({
        values: nextProps.values
      })
    }
  }

  onChange(index, value) {
    const newValues = [
      ...this.state.values.slice(0, index),
      value,
      ...this.state.values.slice(index + 1)
    ];

    this.setState({
      values: newValues
    });

    if (this.props.onChange) {
      this.props.onChange(newValues);
    }
  }

  onAdd() {
    const newItem = '';

    this.setState((prevState, props) => ({
      values: [...prevState.values, newItem]
    }));
  }

  onDelete(index) {
    this.setState((prevState, props) => ({
      values: [
        ...prevState.values.slice(0, index),
        ...prevState.values.slice(index + 1)
      ]
    }), () => { this.props.onChange(this.state.values); });
  }


  render() {
    const recordsCount = this.state.values && this.state.values.length;
    return (
      <div className="form-group row multiline-editor-outer">
        <label className="control-label col-sm-3" data-required="false">{this.props.label}</label>
        <div className="col-sm-9 multiline-editor">
          {this.state.values && this.state.values.length > 0 && this.state.values.map((item, index) => {
              return (
                <div className="row multiline-editor-row" key={`${this.props.id}-${index}`}>
                  <div className="col-xs-6 col-sm-6 col-md-6 col-lg-6">
                    <input type="text" value={item} className="form-control" onChange={(event) => {this.onChange(index, event.target.value)}} />
                  </div>
                  <div className="col-xs-1 col-sm-1 col-md-1 col-lg-1">
                    <nobr>
                      {recordsCount !== 1 &&
                        <Icon name="remove" className="remove" onClick={() => {this.onDelete(index)}} />
                      }
                      {recordsCount === (index + 1) &&
                        <Icon name="plus-circle" className="add" onClick={this.onAdd} />
                      }
                    </nobr>
                  </div>
                </div>
              )
            }
          )}
        </div>
      </div>
    );
  }
}
