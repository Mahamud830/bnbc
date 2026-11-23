# BNBC ULTIMATE PRO - MVP 0.1 : Installer বানানোর নির্দেশনা

## ধাপ ১: GitHub-এ Upload (একবারই)
1. github.com এ Login করুন → ডানে উপরে **+** → **New repository**।
2. নাম দিন `bnbc-ultimate-pro` → **Create repository**।
3. নতুন পাতায় **uploading an existing file** লিংকে ক্লিক করুন।
4. এই ZIP **Extract** করুন। Extract হওয়া ফোল্ডারের ভেতরের **সব ফাইল ও ফোল্ডার** (`src`, `installer`, `.github`, ইত্যাদি) টেনে এনে Upload পাতায় ছাড়ুন।
   - খেয়াল রাখুন: `.github` ফোল্ডারটি যেন বাদ না পড়ে (লুকানো ফোল্ডার দেখানোর অপশন চালু করুন)।
5. নিচে **Commit changes** চাপুন।

## ধাপ ২: Build
1. উপরে **Actions** ট্যাবে যান। "Build Installer" চলছে দেখবেন (৩–৬ মিনিট)।
2. সবুজ টিক ✔ এলে ওই রানে ক্লিক করুন।
3. নিচে **Artifacts** এ `BNBC_Ultimate_Pro_Setup` Download করুন (ZIP আসবে, Extract করলে `BNBC_Ultimate_Pro_Setup.exe`)।
4. লাল ✘ হলে ওই পাতার ছবি/লেখা আমাকে পাঠান, আমি ঠিক করে দেব।

## ধাপ ৩: ইনস্টল ও ব্যবহার
1. Setup.exe Double Click করে Install করুন (Windows SmartScreen আসলে *More info → Run anyway*: সফটওয়্যারটি Digitally Signed নয়)।
2. ETABS 22 চালু করে একটি Model খুলুন।
3. BNBC ULTIMATE PRO চালু করে **Detect / Connect ETABS** চাপুন।
4. Project Setup ভরুন → Seismic Parameters দেখুন → Load Patterns পাতায় Preview করে Create করুন।

## ETABS-এ Test করে আমাকে জানান
- Connect হলো কি? (সবুজ বাতি) / কী Error এলো (Log-এর লেখা)।
- Load Pattern তৈরি হলো কি?
