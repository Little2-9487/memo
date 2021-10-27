# 檢查 APP 是使用哪顆 KeyStore 來做簽名

首先，將 APK 裡面的 /META-INF/ANDROID_.RSA(可能也叫做 CERT.RSA, 裡面應該只會有一個 .RSA 檔案) 解壓縮出來。

然後使用以下的指令：

    keytool -printcert -file ANDROID_.RSA

你會得到一些簽證的資訊如下：

     MD5:  B3:4F:BE:07:AA:78:24:DC:CA:92:36:FF:AE:8C:17:DB
     SHA1: 16:59:E7:E3:0C:AA:7A:0D:F2:0D:05:20:12:A8:85:0B:32:C5:4F:68
     Signature algorithm name: SHA1withRSA
    
然後一樣使用 keytool 的指令，來顯示你使用的 keystore 的資訊。

    keytool -list -keystore my-signing-key.keystore

你會得到這個 keystore 的簽證資訊可能如下：

    android_key, Jan 23, 2010, PrivateKeyEntry,
    Certificate fingerprint (MD5): B3:4F:BE:07:AA:78:24:DC:CA:92:36:FF:AE:8C:17:DB

仔細觀察相對應的簽證資訊是否一致即可。

Keytool 有使用到 java 所以使用前要先確定有設定好 PATH (環境變數)

ref: https://stackoverflow.com/questions/11331469/how-do-i-find-out-which-keystore-was-used-to-sign-an-app