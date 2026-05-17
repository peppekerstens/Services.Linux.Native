# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability in this module, please report it
privately by emailing **peppekerstens@hotmail.com**.

**Do not open a public issue** for security vulnerabilities.

You will receive a response within 48 hours. If the issue is confirmed, a fix
will be released as soon as possible.

## Scope

Security issues include:
- Privilege escalation vulnerabilities in cmdlets
- Unsafe handling of credentials or secrets
- Path traversal or injection in file operations
- Any issue that could allow unauthorized access to system resources

## Out of Scope

The following are not considered security vulnerabilities:
- Missing elevation checks (these are tracked as bugs, not security issues)
- D-Bus polkit error handling (these are user-experience issues)
- Test failures or CI issues
