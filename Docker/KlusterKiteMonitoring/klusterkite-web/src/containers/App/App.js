import React from 'react'

import { browserHistory } from 'react-router'
import { IndexLink } from 'react-router';
import { LinkContainer } from 'react-router-bootstrap';
import Navbar from 'react-bootstrap/lib/Navbar';
import Nav from 'react-bootstrap/lib/Nav';
import NavItem from 'react-bootstrap/lib/NavItem';
import NavDropdown from 'react-bootstrap/lib/NavDropdown';

import { hasPrivilege } from '../../utils/privileges';
import Storage from '../../utils/ttl-storage';

import './App.css';

export default class App extends React.Component {
  render () {
    const username = this.getUsername();

    return (
      <div>
        <Navbar fixedTop>
          <Navbar.Header>
            <Navbar.Brand>
              <IndexLink to="/klusterkite/" activeStyle={{ color: '#333' }}>
                <div className="topLogo" />
                <span>KlusterKite</span>
              </IndexLink>
            </Navbar.Brand>
          </Navbar.Header>
          <Navbar.Collapse>
            <Nav navbar>
              {false && <LinkContainer to="/klusterkite/GraphQL">
                <NavItem>GraphQL</NavItem>
              </LinkContainer>
              }
              {hasPrivilege('KlusterKite.NodeManager.Configuration.GetList') &&
              <LinkContainer to="/klusterkite/Configurations">
                <NavItem>Configurations</NavItem>
              </LinkContainer>
              }
              {(hasPrivilege('KlusterKite.NodeManager.User.GetList') || hasPrivilege('KlusterKite.NodeManager.Role.GetList')) &&
                <NavDropdown title="Users & Roles" id="basic-nav-dropdown">
                  {hasPrivilege('KlusterKite.NodeManager.User.GetList') &&
                  <LinkContainer to="/klusterkite/Users">
                    <NavItem>Users</NavItem>
                  </LinkContainer>
                  }
                  {hasPrivilege('KlusterKite.NodeManager.Role.GetList') &&
                  <LinkContainer to="/klusterkite/Roles">
                    <NavItem>Roles</NavItem>
                  </LinkContainer>
                  }
                </NavDropdown>
              }
              {hasPrivilege('KlusterKite.Monitoring.GetClusterTree') &&
              <LinkContainer to="/klusterkite/ActorsTree">
                <NavItem>Actors Tree</NavItem>
              </LinkContainer>
              }
              <NavDropdown title="…" id="basic-nav-dropdown">
                <LinkContainer to={`/klusterkite/ValidateState/?from=${encodeURIComponent(browserHistory.getCurrentLocation().pathname)}`}>
                  <NavItem>Validate State</NavItem>
                </LinkContainer>
              </NavDropdown>
            </Nav>
            {username &&
              <Nav pullRight>
                <NavDropdown title={username} id="basic-nav-dropdown">
                  <LinkContainer to="/klusterkite/Logout">
                    <NavItem href="#">Logout ({username})</NavItem>
                  </LinkContainer>
                  <LinkContainer to="/klusterkite/ChangePassword">
                    <NavItem href="#">Change Password</NavItem>
                  </LinkContainer>
                </NavDropdown>
              </Nav>
            }
          </Navbar.Collapse>
        </Navbar>
        <div className="container app">
          {this.props.children}
        </div>
      </div>
    )
  }

  /**
   * Gets current authorized username from the local storage
   * @return {string} username
   */
  getUsername() {
    const refreshToken = Storage.get('refreshToken');
    let username = null;
    if (refreshToken) {
      username = Storage.get('username');
      if (!username) {
        username = 'user';
      }
    }
    return username;
  }
}
