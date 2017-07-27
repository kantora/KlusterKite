import React from 'react';
import { Input, Textarea } from 'formsy-react-components';

import Form from '../Form/Form';
import RowText from '../Form/RowText';

import DateFormat from '../../utils/date';

export default class ConfigurationForm extends React.Component {
  constructor(props) {
    super(props);
    this.submit = this.submit.bind(this);
  }

  static propTypes = {
    onSubmit: React.PropTypes.func.isRequired,
    onDelete: React.PropTypes.func,
    initialValues: React.PropTypes.object,
    saving: React.PropTypes.bool,
    deleting: React.PropTypes.bool,
    saved: React.PropTypes.bool,
    saveError: React.PropTypes.string,
    saveErrors: React.PropTypes.arrayOf(React.PropTypes.string),
    canEdit: React.PropTypes.bool,
    activeConfiguration: React.PropTypes.object,
  };

  submit(model) {
    model.minorVersion = model.minorVersion ? Number.parseInt(model.minorVersion, 10) : 0;
    model.majorVersion = model.majorVersion ? Number.parseInt(model.majorVersion, 10) : 0;
    this.props.onSubmit(model);
  }

  render() {
    const { initialValues, activeConfiguration } = this.props;
    const canEdit = !initialValues || initialValues.state === 'Draft';

    return (
      <div>
        {initialValues && canEdit &&
          <h2>Edit Configuration</h2>
        }
        {initialValues && !canEdit &&
          <h2>View Configuration</h2>
        }
        {!initialValues &&
          <h2>Create a new Configuration</h2>
        }
        <Form
          onSubmit={this.submit}
          onDelete={this.props.onDelete ? this.props.onDelete : null}
          className="form-horizontal form-margin"
          saving={this.props.saving}
          deleting={this.props.deleting}
          saved={this.props.saved}
          saveError={this.props.saveError}
          saveErrors={this.props.saveErrors}
          forbidEdit={!this.props.canEdit}
        >
          <fieldset>
            <Input name="__id" value={(initialValues && initialValues.__id) || ""} type="hidden" />
            <Input name="name" label="Name" value={(initialValues && initialValues.name) || (activeConfiguration && `Release ${activeConfiguration.majorVersion}.${activeConfiguration.minorVersion+1}`) || ""} required />
            <Textarea name="notes" label="Notes" value={(initialValues && initialValues.notes) || ""} rows={3} />
            <Input name="majorVersion" label="Major version" value={(initialValues && initialValues.majorVersion.toString()) || (activeConfiguration && (activeConfiguration.majorVersion).toString()) || ""}
                   required
                   validations={{isNumeric:true}}
                   validationErrors={{isNumeric: 'You have to type a number'}}
                   elementWrapperClassName="col-sm-2" />
            <Input name="minorVersion" label="Minor version" value={(initialValues && initialValues.minorVersion.toString()) || (activeConfiguration && (activeConfiguration.minorVersion+1).toString()) || ""}
                   required
                   validations={{isNumeric:true}}
                   validationErrors={{isNumeric: 'You have to type a number'}}
                   elementWrapperClassName="col-sm-2" />
            <RowText label="State" text={(initialValues && initialValues.state) || "Draft"} />
            <RowText label="Created" text={(initialValues && initialValues.created && DateFormat.formatDateTime(new Date(initialValues.created))) || "—"} />
            <RowText label="Finished" text={(initialValues && initialValues.finished && DateFormat.formatDateTime(new Date(initialValues.finished))) || "—"} />
          </fieldset>
        </Form>
      </div>
    );
  }
}
