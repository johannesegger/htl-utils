import { InteractionRequiredAuthError, PublicClientApplication, type Configuration } from '@azure/msal-browser'

const msalConfig: Configuration = {
    auth: {
        clientId: 'fb763808-3d92-4310-896c-fc03fbd854b8',
        authority: 'https://login.microsoftonline.com/htlvb.at',
        redirectUri: window.location.origin // because redirection could happen from every sub-url
    },
}

const msalInstance = await PublicClientApplication.createPublicClientApplication(msalConfig)

const defaultScopes = [ "api://235fe3a7-8dbd-426c-b7b1-3d64cb37724b/.default" ]

const redirectPromise = msalInstance.handleRedirectPromise()
redirectPromise.then((tokenResponse) => {
    if (tokenResponse) {
        msalInstance.setActiveAccount(msalInstance.getAllAccounts()[0])
    }
    else {
        msalInstance.loginRedirect({ scopes: defaultScopes })
    }
}).catch(error => {
    console.error('Error while logging in', error)
})

async function login(scopes: string[]) : Promise<void> {
    if (msalInstance.getAllAccounts().length !== 0) {
        return
    }
    try {
        await msalInstance.loginRedirect({scopes: [...defaultScopes, ...scopes]})
    } catch (error) {
        console.error('Error while logging in', error)
    }
}

async function tryAcquireTokenSilent(scopes: string[]) : Promise<string | void> {
    if (msalInstance.getAllAccounts().length !== 1) {
        return
    }
    try {
        const response = await msalInstance.acquireTokenSilent({scopes: [...defaultScopes, ...scopes]})
        return response.accessToken
    }
    catch (error) {
        if (error instanceof InteractionRequiredAuthError) {
            console.log('Interaction required', error)
            await msalInstance.acquireTokenRedirect({scopes: [...defaultScopes, ...scopes]})
        } else {
            console.warn('Error while getting access token', error)
        }
    }
}

async function tryAcquireToken(scopes: string[]) : Promise<void> {
    await msalInstance.acquireTokenRedirect({scopes: [...defaultScopes, ...scopes]})
}

async function getAccessToken(scopes: string[]) : Promise<string> {
    await redirectPromise
    await login(scopes)
    const accessToken = await tryAcquireTokenSilent(scopes)
    if (accessToken) {
        return accessToken
    }
    await tryAcquireToken(scopes)
    throw "Couldn't get access token: Redirect didn't happen"
}

async function getFetchHeaderWithAccessToken(scopes: string[]) : Promise<{ 'Authorization': string }> {
    const accessToken = await getAccessToken(scopes)
    return { 'Authorization': `Bearer ${accessToken}` }
}

async function tryGetLoggedInUser() {
    await redirectPromise
    const account = msalInstance.getActiveAccount()
    if (!account) {
        return
    }
    return account.username
}

function logout() {
    msalInstance.logout()
}

export { login, logout, getAccessToken, getFetchHeaderWithAccessToken, tryGetLoggedInUser }
