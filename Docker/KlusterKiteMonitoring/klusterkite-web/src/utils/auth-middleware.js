import Storage from './ttl-storage';
import { browserHistory } from 'react-router'

const doRedirect = true;

/**
 * Redirects to the authorization page (in case everything else fails)
 * @param reject {Function} Callback function for failure
 */
const redirectToAuth = (reject) => {
  const currentLocation = browserHistory.getCurrentLocation().pathname;
  const currentSearch = browserHistory.getCurrentLocation().search;

  Storage.remove('privileges');
  Storage.remove('accessToken');
  Storage.remove('refreshToken');
  Storage.remove('username');

  if (currentLocation.indexOf('/klusterkite/Login') === -1 && doRedirect) {
    browserHistory.push(`/klusterkite/Login/?from=${encodeURIComponent(currentLocation + currentSearch)}`);
  }
  if (reject){
    reject();
  }
};

/**
 * Refreshing access token with refresh token, see
 * https://www.oauth.com/oauth2-servers/access-tokens/refreshing-access-tokens/
 * @param {string} refreshToken Refresh token
 */
const requestNewToken = (refreshToken => {
  const url = process.env.REACT_APP_AUTH_URL;
  const payload = `grant_type=refresh_token&client_id=KlusterKite.NodeManager.WebApplication&refresh_token=${refreshToken}`;

  return fetch(url, {
    method: 'post',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: payload,
    mode: 'cors'
  });
});

/**
 * Save authentication data to the local storage
 * @param data {Object} - Server response
 * @param data.access_token {string} - Access token
 * @param data.refresh_token {string} - Refresh token
 * @param data.expires_in {number} - Access token's expiration time, in seconds
 * @param data.token_type {string} - Represents how an access_token will be generated and presented
 */
const authenticate = (data) => {
  Storage.set('accessToken', data.access_token, data.expires_in * 1000);
  Storage.set('refreshToken', data.refresh_token);
};

/**
 * Process new token from the server and resolves new access token
 * @param response {Object} Authorization data from the server
 * @param resolve {Function} Resolve function
 * @param reject {Function} Callback function for failure
 */
const processResponse = (response, resolve, reject) => {
  if (response.status === 200) {
    response.json().then(function(data) {
      authenticate(data);
      resolve(data.access_token);
    });
  } else {
    redirectToAuth(reject);
  }
};

/**
 * Gets a new access token from the server, returns a Promise
 * @return {Promise} Promise for a new token
 */
const tokenRefreshPromise = () => {
  const promise = new Promise((resolve, reject) => {
    const refreshToken = Storage.get('refreshToken');

    if (refreshToken) {
      if (refreshToken) {
        requestNewToken(refreshToken).then(data => {
          processResponse(data, resolve, reject);
        }).catch(error => {
          console.log(error);
          redirectToAuth(reject);
        });
      } else {
        redirectToAuth(reject);
      }
    }

    if (!refreshToken) {
      redirectToAuth(reject);
    }
  });

  return promise;
};

/**
 * Gets the access token or redirects to the authorization page
 * @return {String} Access token
 */
const getToken = () => {
  const accessToken = Storage.get('accessToken');
  const refreshToken = Storage.get('refreshToken');

  if (!accessToken && !refreshToken) {
    redirectToAuth(null);
  }
  else if (!accessToken) {
    return tokenRefreshPromise().then(function() {
      return Storage.get('accessToken');
    });
  }
  else {
    return accessToken;
  }
};

export default {
  getToken: getToken.bind(this),
  tokenRefreshPromise: tokenRefreshPromise.bind(this),
};
