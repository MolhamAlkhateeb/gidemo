export interface AppConfig {
  authEnabled: boolean;
  region: string;
  userPoolClientId: string;
}

let config: AppConfig | null = null;

export async function loadConfig(): Promise<AppConfig> {
  if (config) return config;
  const res = await fetch("/api/config");
  config = (await res.json()) as AppConfig;
  return config;
}

const TOKEN_KEY = "idToken";

export const getToken = () => localStorage.getItem(TOKEN_KEY);
export const setToken = (t: string) => localStorage.setItem(TOKEN_KEY, t);
export const logout = () => {
  localStorage.removeItem(TOKEN_KEY);
  location.reload();
};

interface AuthResult {
  AuthenticationResult?: { IdToken: string };
  ChallengeName?: string;
  Session?: string;
}

function cognitoUrl(region: string) {
  return `https://cognito-idp.${region}.amazonaws.com/`;
}

async function cognitoCall(region: string, target: string, body: unknown) {
  const res = await fetch(cognitoUrl(region), {
    method: "POST",
    headers: {
      "Content-Type": "application/x-amz-json-1.1",
      "X-Amz-Target": `AWSCognitoIdentityProviderService.${target}`,
    },
    body: JSON.stringify(body),
  });
  const data = await res.json();
  if (!res.ok) {
    const msg = data.message || data.__type || "Login failed";
    throw new Error(msg);
  }
  return data as AuthResult;
}

/**
 * Signs in with username/password. If the account is in the first-login
 * "new password required" state, pass newPassword to complete it.
 * Returns "OK" on success or "NEW_PASSWORD_REQUIRED" when a new password is needed.
 */
export async function login(
  region: string,
  clientId: string,
  username: string,
  password: string,
  newPassword?: string
): Promise<"OK" | "NEW_PASSWORD_REQUIRED"> {
  const initial = await cognitoCall(region, "InitiateAuth", {
    AuthFlow: "USER_PASSWORD_AUTH",
    ClientId: clientId,
    AuthParameters: { USERNAME: username, PASSWORD: password },
  });

  if (initial.AuthenticationResult?.IdToken) {
    setToken(initial.AuthenticationResult.IdToken);
    return "OK";
  }

  if (initial.ChallengeName === "NEW_PASSWORD_REQUIRED") {
    if (!newPassword) return "NEW_PASSWORD_REQUIRED";
    const responded = await cognitoCall(region, "RespondToAuthChallenge", {
      ChallengeName: "NEW_PASSWORD_REQUIRED",
      ClientId: clientId,
      Session: initial.Session,
      ChallengeResponses: { USERNAME: username, NEW_PASSWORD: newPassword },
    });
    if (responded.AuthenticationResult?.IdToken) {
      setToken(responded.AuthenticationResult.IdToken);
      return "OK";
    }
  }

  throw new Error("Unexpected authentication response");
}
