import React from 'react';

import Relay from 'react-relay'
import useRelay from 'react-router-relay'
import { Router, IndexRoute, Route, Redirect, browserHistory, applyRouterMiddleware } from 'react-router'

import App from './containers/App/App';
import ActorsTreePage from './containers/ActorsTreePage/ActorsTreePage';
import AuthPage from './containers/AuthPage/AuthPage';
import ChangePasswordPage from './containers/ChangePasswordPage/ChangePasswordPage';
import ConfigurationConfigCopyPage from './containers/ConfigCopyPage/ConfigurationConfigCopyPage';
import ConfigurationsListPage from './containers/ConfigurationsListPage/ConfigurationsListPage';
import ConfigurationPage from './containers/ConfigurationPage/ConfigurationPage';
import FeedPage from './containers/FeedPage/FeedPage';
import GetPrivilegesPage from './containers/GetPrivilegesPage/GetPrivilegesPage';
import GraphQLPage from './containers/GraphQL/GraphQLPage';
import HomePage from './containers/Home/HomePage';
import Loading from './components/Loading/index';
import LogoutPage from './containers/LogoutPage/LogoutPage';
import MigrationPage from './containers/MigrationPage/MigrationPage';
import MigratorTemplatePage from './containers/MigratorTemplatePage/TemplatePage';
import NodeTemplatePage from './containers/NodeTemplatePage/TemplatePage';
import NotFoundPage from './containers/NotFoundPage/NotFoundPage';
import PackagesPage from './containers/PackagesPage/PackagesPage';
import ResetPasswordPage from './containers/UserPage/ResetPasswordPage';
import RolesListPage from './containers/RolesListPage/RolesListPage';
import RolePage from './containers/RolePage/RolePage';
import SeedPage from './containers/SeedPage/SeedPage';
import UsersListPage from './containers/UsersListPage/UsersListPage';
import UserPage from './containers/UserPage/UserPage';
import ValidateStatePage from './containers/ValidateStatePage/ValidateStatePage';

export default class RoutesList extends React.Component {
  render () {
    const ApiQueries = { api: () => Relay.QL`query {
      api
    }` };

    return (
      <Router forceFetch environment={Relay.Store} render={applyRouterMiddleware(useRelay)} history={browserHistory}>
        <Route path="/klusterkite/" component={App}>
          <IndexRoute component={HomePage} queries={ApiQueries} />
          <Route path='/klusterkite/Home/:sort' component={HomePage} queries={ApiQueries} />
          <Route path='/klusterkite/Login' component={AuthPage} />
          <Route path='/klusterkite/Logout' component={LogoutPage} />
          <Route path='/klusterkite/ChangePassword' component={ChangePasswordPage} queries={ApiQueries} render={({ props }) => props ? <ChangePasswordPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/GetPrivileges' component={GetPrivilegesPage} queries={ApiQueries} />
          <Route path='/klusterkite/ActorsTree' component={ActorsTreePage} queries={ApiQueries} />
          <Route path='/klusterkite/CopyConfig/:configurationId/:mode' component={ConfigurationConfigCopyPage} queries={ApiQueries} render={({ props }) => props ? <ConfigurationConfigCopyPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/GraphQL' component={GraphQLPage} />
          <Route path='/klusterkite/Migration' component={MigrationPage} queries={ApiQueries} />
          <Route path='/klusterkite/NugetFeeds/:configurationId' component={FeedPage} queries={ApiQueries} render={({ props }) => props ? <FeedPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Configurations' component={ConfigurationsListPage} queries={ApiQueries} render={({ props }) => props ? <ConfigurationsListPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Configurations/:page' component={ConfigurationsListPage} queries={ApiQueries} render={({ props }) => props ? <ConfigurationsListPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Configuration/create/:mode' component={ConfigurationPage} queries={ApiQueries} render={({ props }) => props ? <ConfigurationPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Configuration/:id' component={ConfigurationPage} queries={ApiQueries} render={({ props }) => props ? <ConfigurationPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Roles' component={RolesListPage} queries={ApiQueries} render={({ props }) => props ? <RolesListPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Roles/create' component={RolePage} queries={ApiQueries} render={({ props }) => props ? <RolePage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Roles/:id' component={RolePage} queries={ApiQueries} render={({ props }) => props ? <RolePage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Seeds/:configurationId' component={SeedPage} queries={ApiQueries} render={({ props }) => props ? <SeedPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/MigratorTemplates/:configurationId/create' component={MigratorTemplatePage} queries={ApiQueries} render={({ props }) => props ? <MigratorTemplatePage {...props} /> : <Loading />} />
          <Route path='/klusterkite/MigratorTemplates/:configurationId/:id' component={MigratorTemplatePage} queries={ApiQueries} render={({ props }) => props ? <MigratorTemplatePage {...props} /> : <Loading />} />
          <Route path='/klusterkite/NodeTemplates/:configurationId/create' component={NodeTemplatePage} queries={ApiQueries} render={({ props }) => props ? <NodeTemplatePage {...props} /> : <Loading />} />
          <Route path='/klusterkite/NodeTemplates/:configurationId/:id' component={NodeTemplatePage} queries={ApiQueries} render={({ props }) => props ? <NodeTemplatePage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Packages/:configurationId' component={PackagesPage} queries={ApiQueries} render={({ props }) => props ? <PackagesPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Users' component={UsersListPage} queries={ApiQueries} render={({ props }) => props ? <UsersListPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Users/create' component={UserPage} queries={ApiQueries} render={({ props }) => props ? <UserPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Users/:id' component={UserPage} queries={ApiQueries} render={({ props }) => props ? <UserPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/Users/ResetPassword/:id' component={ResetPasswordPage} queries={ApiQueries} render={({ props }) => props ? <ResetPasswordPage {...props} /> : <Loading />} />
          <Route path='/klusterkite/ValidateState' component={ValidateStatePage} render={({ props }) => props ? <ValidateStatePage {...props} /> : <Loading />} />
          <Route path='*' components={NotFoundPage} />
        </Route>
        <Redirect from="/" to="klusterkite/" />
      </Router>
    )
  }
}
