import Relay from 'react-relay'

export default class ValidateStateMutation extends Relay.Mutation {

  getMutation () {
    return Relay.QL`mutation{klusterKiteNodeApi_klusterKiteNodesApi_clusterManagement_recheckState}`
  }

  getFatQuery () {
    return Relay.QL`
      fragment on Boolean_MutationPayload {
        result
      }
    `
  }

  getConfigs() {
    return [{
      type: 'REQUIRED_CHILDREN',
      children: [
          Relay.QL`
          fragment on Boolean_MutationPayload {
            result
          }
        `,
      ],
    }];
  }

  getVariables () {
    return {}
  }

  getOptimisticResponse () {
    return {
      result: {
        result: true
      }
    }
  }
}

