#!/usr/bin/env python3
"""
Generate Amazon SES SMTP password from IAM secret access key.  A good
chunk of this script was taken from the AWS documentation.
See: https://docs.aws.amazon.com/ses/latest/dg/smtp-credentials.html
"""
import sys
import json
import hmac
import hashlib
import base64


def sign(key, msg):
    return hmac.new(key, msg.encode("utf-8"), hashlib.sha256).digest()


def calculate_smtp_password(secret_access_key: str, region: str) -> str:
    DATE = "11111111"
    SERVICE = "ses"
    MESSAGE = "SendRawEmail"
    TERMINAL = "aws4_request"
    VERSION = 0x04

    signature = sign(("AWS4" + secret_access_key).encode("utf-8"), DATE)
    signature = sign(signature, region)
    signature = sign(signature, SERVICE)
    signature = sign(signature, TERMINAL)
    signature = sign(signature, MESSAGE)

    signature_and_version = bytes([VERSION]) + signature
    return base64.b64encode(signature_and_version).decode("utf-8")


def main():
    data = json.load(sys.stdin)
    secret_key = data.get("secret_key", "")
    region = data.get("region", "us-east-1")
    smtp_password = calculate_smtp_password(secret_key, region)
    print(json.dumps({"smtp_password": smtp_password}))


if __name__ == "__main__":
    main()
