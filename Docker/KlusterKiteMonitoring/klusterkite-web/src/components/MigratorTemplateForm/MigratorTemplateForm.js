import React from 'react';
import { Input, Textarea } from 'formsy-react-components';
import isEqual from 'lodash/isEqual';

import Form from '../Form/Form';
import PackagesMultiSelector from '../PackageSelector/multiselector';

export default class MigratorTemplateForm extends React.Component { // eslint-disable-line react/prefer-stateless-function
  constructor(props) {
    super(props);
    this.submit = this.submit.bind(this);

    this.state = {
      packagesList: []
    };
  }

  static propTypes = {
    onSubmit: React.PropTypes.func.isRequired,
    onCancel: React.PropTypes.func,
    onDelete: React.PropTypes.func,
    initialValues: React.PropTypes.object,
    packagesList: React.PropTypes.object,
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
      const packageRequirements = nextProps.initialValues.packageRequirements.edges.map(x => x.node).map(x => {
        return {
          id: x.__id,
          specificVersion: x.specificVersion
        }
      });

      this.setState({
        packageRequirements: packageRequirements
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

  onPackageRequirementsChange(data) {
    this.setState({
      packageRequirements: data
    });
  }

  submit(model) {
    model.packageRequirements = this.state.packageRequirements;
    model.priority = model.priority ? Number.parseInt(model.priority, 10) : 0;
    this.props.onSubmit(model);
  }

  cancel() {
    this.props.onCancel();
  }

  render() {
    const { initialValues } = this.props;

    return (
      <div>
        {initialValues &&
          <h2>Edit Template</h2>
        }
        {!initialValues &&
          <h2>Create a new Template</h2>
        }
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
            <Input name="code" label="Code" value={(initialValues && initialValues.code) || ""} required />
            <Input name="name" label="Name" value={(initialValues && initialValues.name) || ""} required />
            {this.props.packagesList &&
              <PackagesMultiSelector packages={this.props.packagesList} values={this.state.packageRequirements} onChange={this.onPackageRequirementsChange.bind(this)} />
            }
            <Input name="priority" label="Priority" value={(initialValues && initialValues.priority) || ""} validations="isNumeric" validationError="Must be numeric" elementWrapperClassName="col-sm-2" />
            <Textarea name="notes" label="Notes" value={(initialValues && initialValues.notes) || ""} rows={3} />
            <Textarea name="configuration" label="Configuration" value={(initialValues && initialValues.configuration) || ""} rows={10} />
          </fieldset>
        </Form>
      </div>
    );
  }
}
