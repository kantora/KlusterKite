import React from 'react';
import Relay from 'react-relay'
import debounce from 'lodash/debounce'
import isEqual from 'lodash/isEqual';

import UpdateFeedMutation from '../../containers/FeedPage/mutations/UpdateFeedMutation'

import './styles.css';

class FeedList extends React.Component {
  constructor (props) {
    super(props);
    this.onStartEdit = this.onStartEdit.bind(this);
    this.onChange = this.onChange.bind(this);
    this.saveData = this.saveData.bind(this);
    this.onSaveChanges = debounce(this.onSaveChanges, 1500);

    this.state = {
      isEditing: false,
      nugetFeed: '',
    }
  }

  static propTypes = {
    configurationId: React.PropTypes.string,
    configurationInnerId: React.PropTypes.number,
    settings: React.PropTypes.object,
    canEdit: React.PropTypes.bool
  };

  componentWillMount() {
    this.onReceiveProps(this.props, true);
  }

  componentWillReceiveProps(nextProps) {
    this.onReceiveProps(nextProps, false);
  }

  onReceiveProps(nextProps, skipCheck) {
    // We save unfiltered packages list to use later in migration
    if (nextProps.settings && (!isEqual(nextProps.settings.nugetFeed, this.props.settings.nugetFeed) || skipCheck)) {
      this.setState({
        nugetFeed: nextProps.settings.nugetFeed
      })
    }
  }

  onStartEdit() {
    this.setState({
      isEditing: true,
    })
  }

  onChange(value) {
    this.setState({
      nugetFeed: value
    });
  }

  onSaveChanges(value) {
    this.saveData(value);
  }

  saveData(value){
    this.setState({
      saving: true
    });

    Relay.Store.commitUpdate(
      new UpdateFeedMutation(
        {
          nodeId: this.props.configurationId,
          configurationId: this.props.configurationInnerId,
          settings: this.props.settings,
          nugetFeed: value,
        }),
      {
        onSuccess: (response) => {
          if (response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors &&
            response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors.edges) {
            const messages = this.getErrorMessagesFromEdge(response.klusterKiteNodeApi_klusterKiteNodesApi_configurations_update.errors.edges);

            this.setState({
              saving: false,
              saveErrors: messages
            });
          } else {
            // ok
            this.setState({
              saving: false,
              isEditing: false,
            });
          }
        },
        onFailure: (transaction) => {
          this.setState({
            saving: false
          });
          console.log(transaction)},
      },
    )
  }

  render() {
    return (
      <div>
        <div>
          <h3>Nuget feeds</h3>

          <p>
            {this.props.canEdit && this.state.isEditing &&
            <input type="text" className="form-control" value={this.state.nugetFeed} onChange={(event) => {this.onChange(event.target.value)}} onBlur={(event) => {this.saveData(event.target.value)}} />
            }
            {this.props.canEdit && !this.state.isEditing &&
            <span className="pseudohref" onClick={this.onStartEdit}>
              {this.state.nugetFeed}
            </span>
            }

            {!this.props.canEdit &&
              <span>{this.state.nugetFeed}</span>
            }
          </p>
        </div>
      </div>
    );
  }
}

export default Relay.createContainer(
  FeedList,
  {
    fragments: {
      settings: () => Relay.QL`fragment on IKlusterKiteNodeApi_ConfigurationSettings {
        nugetFeed,
        ${UpdateFeedMutation.getFragment('settings')},
      }
      `,
    },
  },
)
