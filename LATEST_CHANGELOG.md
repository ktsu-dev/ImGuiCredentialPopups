## v1.4.0 (minor)

Changes since v1.3.0:

- Count a selection's room when deciding whether a paste fits, and put it back when the paste is rejected [patch] (#223) (matt-edmondson)
- Hand a confirmed credential to onConfirm after the modal is drawn [patch] (#224) (matt-edmondson)
- Keep OK and Enter from confirming a blank token, username or password [minor] (#225) (matt-edmondson)
- Let a username and password popup open with the username filled in [minor] (#226) (matt-edmondson)
- Report a paste too long for a credential field, and don't confirm while it shows [patch] (#215) (matt-edmondson)
- Trim a pasted token and username before returning them [patch] (#213) (matt-edmondson)
- Add CompatibilitySuppressions.xml for the Polyfill 11.4.5 shim drift [patch] (#211) (matt-edmondson)
- Accept a credential of exactly MaxCredentialLength bytes [patch] (matt-edmondson)
- Start a fresh showing on every Open, even while the popup is up [patch] (Claude)
- Show a popup from the gallery in the README (Claude)

