import Keycloak from 'keycloak-js'

const keycloak = new Keycloak({
    url: 'https://id.htlvb.at',
    realm: 'htlvb',
    clientId: 'htl-utils'
})

try {
    const authenticated = await keycloak.init({ onLoad: 'login-required' })
    if (import.meta.env.DEV) {
        if (authenticated) {
            console.log('User is authenticated')
        } else {
            console.log('User is not authenticated')
        }
    }
} catch (error) {
    console.error('Failed to initialize adapter:', error)
}

async function getAccessToken() : Promise<string> {
    try {
        const tokenRefreshed = await keycloak.updateToken()
        if (import.meta.env.DEV) {
            if (tokenRefreshed) {
                console.log('Access token refreshed')
            }
            else {
                console.log('No need to refresh access token')
            }
        }
    } catch (error) {
        console.error('Failed to refresh token:', error)
    }
    if (keycloak.token === undefined) {
        throw 'Failed to get access token'
    }
    return keycloak.token
}

async function getFetchHeaderWithAccessToken() : Promise<{ 'Authorization': string }> {
    const accessToken = await getAccessToken()
    return { 'Authorization': `Bearer ${accessToken}` }
}

async function tryGetLoggedInUser() : Promise<string | undefined> {
    try {
        const user = await keycloak.loadUserProfile()
        return user.username
    }
    catch (error) {
        console.error('Failed to load user profile', error)
    }
}

async function login() : Promise<void> {
    await keycloak.login()
}

async function logout() : Promise<void> {
    await keycloak.logout()
}

export { login, logout, getAccessToken, getFetchHeaderWithAccessToken, tryGetLoggedInUser }
