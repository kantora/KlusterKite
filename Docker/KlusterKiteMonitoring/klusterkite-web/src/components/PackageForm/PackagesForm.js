import React from 'react';
import isEqual from 'lodash/isEqual';

import Form from '../Form/Form';
import PackagesMultiSelector from '../PackageSelector/PackagesMultiSelector';

export default class PackagesForm extends React.Component { // eslint-disable-line react/prefer-stateless-function
  constructor(props) {
    super(props);
    this.submit = this.submit.bind(this);

    this.state = {
      model: {}
    };
  }

  static propTypes = {
    onSubmit: React.PropTypes.func.isRequired,
    onCancel: React.PropTypes.func,
    onDelete: React.PropTypes.func,
    initialValues: React.PropTypes.object,
    saving: React.PropTypes.bool,
    deleting: React.PropTypes.bool,
    saved: React.PropTypes.bool,
    saveErrors: React.PropTypes.arrayOf(React.PropTypes.string),
    saveError: React.PropTypes.string,
  };

  componentWillMount() {
    this.onReceiveProps(this.props, true);
  }

  componentWillReceiveProps(nextProps) {
    this.onReceiveProps(nextProps, false);
  }

  onReceiveProps(nextProps, skipCheck) {
    if (nextProps.initialValues && (!isEqual(nextProps.initialValues, this.props.initialValues) || skipCheck)) {
      const packages = nextProps.initialValues.packages.edges.map(x => x.node).map(x => {
        return {
          id: x._id,
          specificVersion: x.version
        }
      });

      this.setState({
        packages: packages
      });
    }
  }

  arrayToString(data) {
    return data && this.replaceAll(data.join(), ',', '\n');
  }

  stringToArray(data) {
    return data && data.length > 0 ? data.split('\n') : [];
  }

  replaceAll(value, search, replacement) {
    return value.replace(new RegExp(search, 'g'), replacement);
  }

  submit(model) {
    const packages = this.state.packages.map(x => {
      return {
        id: x.id,
        version: x.specificVersion
      }
    });

    model.packages = packages;
    this.props.onSubmit(model);
  }

  cancel() {
    this.props.onCancel();
  }

  onPackagesChange(data) {
    this.setState({
      packages: data
    });
  }

  render() {
    return (
      <div>
        <h2>Edit Packages</h2>
        <Form
          onSubmit={this.submit}
          onCancel={this.props.onCancel}
          onDelete={this.props.onDelete ? this.props.onDelete : null}
          className="form-horizontal form-margin"
          saving={this.props.saving}
          deleting={this.props.deleting}
          saved={this.props.saved}
          saveError={this.props.saveError}
          saveErrors={this.props.saveErrors}
        >
          <fieldset>
            {this.props.packagesList &&
              <PackagesMultiSelector
                packages={this.props.packagesList}
                values={this.state.packages}
                onChange={this.onPackagesChange.bind(this)}
              />
            }
          </fieldset>
        </Form>
      </div>
    );
  }
}
