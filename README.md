Built with Unity (AR Foundation / ARCore), FastAPI, and PostgreSQL.

---

## Option 1 Download and install the pre-built APK

This is the quickest way to run the app without installing any development tools.

### Requirements
- Android device running Android 7.0 (API 24) or higher
- ARCore support on your device (check if your device is supported https://developers.google.com/ar/devices)
- Internet connection

### Steps

1. Go to the (https://github.com/FergusC1/drumkit-ar/releases) on GitHub
2. Download drumkitar.apk
3. On your Android device go to Settings → Apps → Special App Access → Install Unknown Apps
4. Find your browser or file manager and enable Allow from this source
5. Open the downloaded APK file and tap Install
6. Find DrumKitAR in your app drawer and open it

---

## Option 2 Build and run from source

### Prerequisites

Before starting, install the following:

- Git https://git-scm.com/download/win
- Python 3.11+ https://www.python.org/downloads/
- PostgreSQL 14+ https://www.postgresql.org/download/
-  Unity Hub https://unity.com/download with Unity 2022.3 LTS when installing Unity make sure to tick:
  - Android Build Support
  - Android SDK & NDK Tools
  - OpenJDK

-You will also need your Android device with ARCore support connected via USB

---

### Step 1 Clone the repository

Open Git Bash and run:

```bash
git clone https://github.com/FergusC1/drumkit-ar.git
cd drumkit-ar
```

---


### Step 2 Open the Unity project

1. Open Unity Hub
2. Click Open and navigate to `drumkit-ar/app/DrumKitAR`

---

### Step 3 Prepare your Android device

1. On your device go to Settings -> About Phone
2. Tap Build Number seven times to enable Developer Mode
3. Go to Settings -> Developer Options
4. Enable USB Debugging
5. Connect your device via USB
6. Tap Allow when the USB debugging prompt appears on your phone

---

### Step 4  Build and run

1. In Unity open File -> Build Settings
2. Confirm Android is selected as the platform
3. Confirm MainScene is in the Scenes In Build list
4. Tick Development Build
5. Select your device in the Run Device dropdown
6. Click Build And Run

---

## Using the app

### Capturing a kit

1. Launch the app and point the camera at the floor
2. Move the phone slowly around the space for at least 30–60 seconds until the scan progress bar completes and shows Ready to calibrate!
3. Place an A4 sheet of paper on a flat surface, press Calibrate, and tap the two opposite corners of the sheet a confidence score will appear
4. Point at a detected plane and tap to place anchor markers at each drum position
5. After placing anchors press Tag Element and select the drum element type
6. Press Save Kit to upload the profile to the backend

### Viewing the schematic

After tagging elements press View Schematic to see a top-down 3D diagram. Drag to rotate, pinch to zoom. Press Back to capture to return to the AR view.

### Guided setup

1. Press Guided Setup
2. Enter a saved profile ID
3. Press Load Profile
4. Tap the floor to place the origin point
5. Blue AR markers will appear at the stored element positions, place anchors near each one and they will turn green when within 15cm

### Sharing a kit

After saving, press Share Kit, select a view level, and press Generate Link. 

---

## Notes

The live backend at `https://drumkit-ar-production.up.railway.app` may not remain active indefinitely after project submission.
To check this go to https://drumkit-ar-production.up.railway.app/health
If it returns {"status":"ok"} then ignore the following, otherwise these instructions show how to create a local database
To run the full system independently, follow the backend setup steps below:

## 1. Set up the backend

```bash
cd drumkit-ar/backend
python -m venv .venv
source .venv/Scripts/activate        # Windows
source .venv/bin/activate            # Mac / Linux
python -m pip install -r requirements.txt
```

Create a `.env` file inside `backend/` with your PostgreSQL credentials:

```
DATABASE_URL=postgresql://postgres:YOURPASSWORD@localhost:5432/drumkit
```

Create the database, run migrations, and start the server:

```bash
alembic upgrade head
uvicorn app.main:app --reload --host 0.0.0.0
```

The API will be available at `http://localhost:8000/docs`.

---

## 2. Open the app

Install the APK on your Android device. 
If building from source, open the Unity project at `drumkit-ar/app/DrumKitAR` in Unity 2022.3 LTS, set the Base Url on the APIClient component to your local IP address 
Then click Build And Run.


