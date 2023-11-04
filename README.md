# Telemed-KMITL-Server

Backends of Telemed-KMITL-App

## System Structure

![SystemStruture](C:\Users\sthai\Projects\Telemed-KMITL-Server\img\SystemStruture.drawio.png)

## Setup

- Copy `.env.example` to `.env`

- Prepare the secret files

  - `secrets/firebase-adminsdk.json`: Service account key for firebase services
    How to create: [Initialize the SDK in non-Google environments (Google)](https://firebase.google.com/docs/admin/setup#initialize_the_sdk_in_non-google_environments)

  - `jitsi-meet-password.env`: Passwords for Jitsi Meet backends

    ```env
    # `openssl rand -hex 16` -> XXX_PASSWORD=[Output text of command]
    JICOFO_AUTH_PASSWORD=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
    JVB_AUTH_PASSWORD=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
    JIGASI_XMPP_PASSWORD=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
    JIBRI_RECORDER_PASSWORD=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
    JIBRI_XMPP_PASSWORD=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
    ```
