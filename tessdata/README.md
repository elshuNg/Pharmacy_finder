# Tesseract language data

Prescription OCR requires English trained data in this folder.

1. Download `eng.traineddata` from the [tessdata repository](https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata).
2. Place the file at `PharmacyFinder.API/tessdata/eng.traineddata`.
3. Confirm `Tesseract:DataPath` in `appsettings.json` points to `tessdata` (default).

The project copies `tessdata/**/*` to the build output directory on build.
