import React from 'react';
import Autosuggest from 'react-autosuggest';
import Icon from 'react-fa';
import { OverlayTrigger, Popover } from 'react-bootstrap';

import isEqual from 'lodash/isEqual';

import './autosuggest.css';
import './packageSelector.css';

export default class PackagesSelector extends React.Component {

  static propTypes = {
    packages: React.PropTypes.object.isRequired,
    onChange: React.PropTypes.func,
    onAdd: React.PropTypes.func,
    onDelete: React.PropTypes.func,
    initialValues: React.PropTypes.object,
    showAlertForSpecificVersions: React.PropTypes.bool,
  };

  constructor() {
    super();

    this.state = {
      packageValue: '',
      packageSuggestions: [],
      packagesNames: [],
      versionValue: '',
      packageVersions: [],
      isPackageValid: false,
      isVersionValid: false
    };
  }

  componentWillMount() {
    this.onReceiveProps(this.props, true);
  }

  componentWillReceiveProps(nextProps) {
    this.onReceiveProps(nextProps, false);
  }

  onReceiveProps(nextProps, skipCheck) {
    if (nextProps.packages && (!isEqual(nextProps.packages, this.props.packages) || skipCheck)) {
      const nodes = nextProps.packages.edges.map(x => x.node);
      const packagesNames = nodes.map(node => node.name);

      this.setState({
        packagesNames: packagesNames
      });
    }

    if (nextProps.initialValues && (!isEqual(nextProps.initialValues, this.props.initialValues) || skipCheck)) {
      this.initInputValuesFromProps(nextProps);
    }
  }

  /**
   * Change package and version values to those passed down in props
   * @param nextProps {Object} New props
   */
  initInputValuesFromProps(nextProps) {
    this.setState({
      packageValue: nextProps.initialValues.id ? nextProps.initialValues.id : '',
      isPackageValid: true,
      versionValue: nextProps.initialValues.specificVersion ? nextProps.initialValues.specificVersion : '',
      isVersionValid: true
    });

    if (this.props.initialValues.id) {
      const nodes = this.props.packages.edges.map(x => x.node);
      const node = nodes.find(x => x.name === this.props.initialValues.id);
      if (node) {
        this.setState({
          packageVersions: node.availableVersions,
        });
      } else {
        console.warn('Package ' + this.props.initialValues.id + ' not found in the packages list!');
      }
    }
  }

  /**
   * Checks typed package name and notifies external component if it has been changed
   * @param event {Event} Event
   * @param newValue {string} Package typed
   */
  onPackageChange = (event, { newValue }) => {
    const nodes = this.props.packages.edges.map(x => x.node);
    const node = nodes.find(x => x.name === newValue);
    const isPackageValid = this.isPackageValid(newValue);

    if (node) {
      this.setState({
        packageValue: newValue,
        versionValue: '',
        packageVersions: node.availableVersions,
        isPackageValid: isPackageValid
      });
    } else {
      this.setState({
        packageValue: newValue,
        versionValue: '',
        packageVersions: [],
        isPackageValid: isPackageValid
      });
    }

    if (isPackageValid && this.props.onChange) {
      this.props.onChange({
        id: newValue,
        specificVersion: null,
      });
    }
  };

  onPackageBlur = () => {
    if (!this.state.isPackageValid) {
      this.setState({
        packageValue: '',
        versionValue: ''
      });
    }
  };

  onVersionBlur = () => {
    if (!this.state.isVersionValid) {
      this.setState({
        versionValue: ''
      });
    }
  };

  /**
   * Checks package list to verify that typed package name is valid
   * @param value {string} Package name typed by user
   * @return {boolean} Is package name typed right?
   */
  isPackageValid = (value) => {
    const valid = this.state.packagesNames.findIndex(x => x === value);
    return valid !== -1;
  };

  /**
   * Checks package list to verify that typed package and version are valid
   * @param value {string} Version typed by user
   * @return {boolean} Are package and version typed right?
   */
  isVersionValid = (value) => {
    const valid = this.state.packageVersions.findIndex(x => x === value);
    return valid !== -1;
  };

  /**
   * Autosuggest will call this function every time you need to update suggestions on packages input.
   * @param value {string} Typed text
   */
  onPackageSuggestionsFetchRequested = ({ value }) => {
    this.setState({
      packageSuggestions: this.getSuggestions(value, this.state.packagesNames)
    });
  };

  /**
   * Autosuggest will call this function every time you need to clear suggestions on packages input.
   */
  onPackageSuggestionsClearRequested = () => {
    this.setState({
      packageSuggestions: []
    });
  };

  // Teach Autosuggest how to calculate suggestions for any given input value.
  getSuggestions = (value, source) => {
    const inputValue = value.trim().toLowerCase();
    const inputLength = inputValue.length;

    return inputLength === 0 ? [] : source.filter(lang =>
      lang.toLowerCase().indexOf(inputValue) !== -1
//      lang.toLowerCase().slice(0, inputLength) === inputValue
    ).slice(0, 10);
  };

  // When suggestion is clicked, Autosuggest needs to populate the input element
  // based on the clicked suggestion. Teach Autosuggest how to calculate the
  // input value for every given suggestion.
  getSuggestionValue = (suggestion) => suggestion;

  // Use your imagination to render suggestions.
  renderSuggestion = (suggestion) => (
    <div>
      {suggestion}
    </div>
  );

  /**
   * Notifies external component that version has been changed
   * @param newValue {string} Version typed
   */
  onChangeVersion(newValue) {
    this.setState({
      versionValue: newValue,
      isVersionValid: true
    });

    if (this.props.onChange) {
      this.props.onChange({
        id: this.state.packageValue,
        specificVersion: newValue,
      });
    }
  }

  render() {
    const { packageValue, packageSuggestions, versionValue } = this.state;

    const popoverHoverFocus = (
      <Popover id="popover-trigger-hover-focus" title="Attention!">
        <p>This package won’t follow the configuration versions update.</p>
        <p>It will be fixed to the specified version.</p>
      </Popover>
    );

    const inputPropsPackage = {
      placeholder: 'Package',
      value: packageValue,
      onChange: this.onPackageChange,
      onBlur: this.onPackageBlur
    };

    return (
      <div className="package-selector">
        <div className="row">
          <div className="col-xs-6 col-sm-6 col-md-6 col-lg-6">
            <Autosuggest
              suggestions={packageSuggestions}
              onSuggestionsFetchRequested={this.onPackageSuggestionsFetchRequested}
              onSuggestionsClearRequested={this.onPackageSuggestionsClearRequested}
              getSuggestionValue={this.getSuggestionValue}
              renderSuggestion={this.renderSuggestion}
              inputProps={inputPropsPackage}
            />
          </div>
          <div className="col-xs-4 col-sm-4 col-md-4 col-lg-4">
            <select className="form-control" defaultValue={versionValue} onChange={(event) => { this.onChangeVersion(event.target.value) }}>
              <option value="">Default value</option>
              {this.state.packageVersions.map((version) => <option key={version} value={version}>{version}</option>)}
            </select>
          </div>
          <div className="col-xs-1 col-sm-1 col-md-1 col-lg-1">
            {versionValue !== '' &&
              <OverlayTrigger trigger={['hover']} placement="bottom" overlay={popoverHoverFocus}>
                <Icon name="exclamation-triangle" className="alert" />
              </OverlayTrigger>
            }
          </div>
          <div className="col-xs-1 col-sm-1 col-md-1 col-lg-1">
            <nobr>
              {this.props.onDelete &&
                <Icon name="remove" className="remove"
                      onClick={this.props.onDelete}
                />
              }
              {this.props.onAdd &&
                <Icon name="plus-circle" className="add"
                      onClick={this.props.onAdd}
                />
              }
            </nobr>
          </div>
        </div>
      </div>
    );
  }
}

