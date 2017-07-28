import React from 'react';
import Formsy from 'formsy-react';
import { Input, Textarea } from 'formsy-react-components';
import isEqual from 'lodash/isEqual';

import Form from '../Form/Form';
import MultiLineEditor from '../Form/MultiLineEditor';
import PackagesMultiSelector from '../PackageSelector/PackagesMultiSelector';

export default class NodeTemplateForm extends React.Component { // eslint-disable-line react/prefer-stateless-function
  constructor(props) {
    super(props);
    this.submit = this.submit.bind(this);

    this.state = {
      packagesList: [],
      containerTypes: [],
    };

    Formsy.addValidationRule('isLessOrEqualThan', function (values, value, otherField) {
      if (isNaN(Number(value))) return true;
      if (isNaN(Number(otherField))) return true;
      return Number(value) <= Number(values[otherField]);
    });

    Formsy.addValidationRule('isMoreOrEqualThan', function (values, value, otherField) {
      if (isNaN(Number(value))) return true;
      if (isNaN(Number(otherField))) return true;
      return Number(value) >= Number(values[otherField]);
    });
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
      const containerTypes = (nextProps.initialValues && nextProps.initialValues.containerTypes) || [];
      const packageRequirements = nextProps.initialValues.packageRequirements.edges.map(x => x.node).map(x => {
        return {
          id: x.__id,
          specificVersion: x.specificVersion
        }
      });

      this.setState({
        packageRequirements: packageRequirements,
        containerTypes: containerTypes,
      });
    }
  }

  onPackageRequirementsChange(data) {
    this.setState({
      packageRequirements: data
    });
  }

  onContainerTypesChange(data) {
    console.log('onContainerTypes', data);

    this.setState({
      containerTypes: data
    });
  }

  submit(model) {
    model.packageRequirements = this.state.packageRequirements;
    model.containerTypes = this.state.containerTypes;
    model.maximumNeededInstances = Number.parseInt(model.maximumNeededInstances, 10);
    model.minimumRequiredInstances = model.minimumRequiredInstances ? Number.parseInt(model.minimumRequiredInstances, 10) : 0;
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
            <Input
              name="minimumRequiredInstances"
              label="Minimum Required Instances"
              value={(initialValues && initialValues.minimumRequiredInstances) || ""}
              validations={{isNumeric:true,isLessOrEqualThan:'maximumNeededInstances'}}
              validationErrors={{isNumeric: 'You have to type a number', isLessOrEqualThan: 'Cannot exceed Maximum Needed Instances'}}
              elementWrapperClassName="col-sm-2"
            />
            <Input
              name="maximumNeededInstances"
              label="Maximum Needed Instances"
              value={(initialValues && initialValues.maximumNeededInstances) || ""}
              validations={{isNumeric:true,isMoreOrEqualThan:'minimumRequiredInstances'}}
              validationErrors={{isNumeric: 'You have to type a number', isMoreOrEqualThan: 'Cannot be less than Minimum Required Instances'}}
              elementWrapperClassName="col-sm-2"
            />
            {this.props.packagesList &&
              <PackagesMultiSelector
                packages={this.props.packagesList}
                values={this.state.packageRequirements}
                onChange={this.onPackageRequirementsChange.bind(this)}
                showAlertForSpecificVersions={true}
              />
            }
            <Input name="priority" label="Priority" value={(initialValues && initialValues.priority) || ""} validations="isNumeric" validationError="Must be numeric" elementWrapperClassName="col-sm-2" />
            <MultiLineEditor id="containerTypes" label="Container Types" values={this.state.containerTypes} onChange={this.onContainerTypesChange.bind(this)} />
            <Textarea name="configuration" label="Configuration" value={(initialValues && initialValues.configuration) || ""} rows={10} />
          </fieldset>
        </Form>
      </div>
    );
  }
}
