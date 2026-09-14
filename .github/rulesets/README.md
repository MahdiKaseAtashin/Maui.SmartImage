# Repository rulesets

GitHub does not load these JSON files automatically. They are the source of truth for [repository rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets). Apply them with the GitHub CLI while authenticated as a repo admin.

The required status check name is `CI / build-test` (workflow `CI`, job `build-test`). Review count is `0` so a solo maintainer can merge.

## Protect `main`

Create:

```sh
gh api --method POST /repos/MahdiKaseAtashin/Maui.SmartImage/rulesets --input .github/rulesets/main-protection.json
```

Update the existing `protect-main` ruleset (id may change; list first):

```sh
gh api /repos/MahdiKaseAtashin/Maui.SmartImage/rulesets
gh api --method PUT /repos/MahdiKaseAtashin/Maui.SmartImage/rulesets/23305238 --input .github/rulesets/main-protection.json
```

## Protect `v*` tags

Blocks tag deletion and force-updates so `pack.yml` is not triggered by rewritten tags.

```sh
gh api --method POST /repos/MahdiKaseAtashin/Maui.SmartImage/rulesets --input .github/rulesets/release-tags.json
```

Update existing tag ruleset:

```sh
gh api --method PUT /repos/MahdiKaseAtashin/Maui.SmartImage/rulesets/23307572 --input .github/rulesets/release-tags.json
```
