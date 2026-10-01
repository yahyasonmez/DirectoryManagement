# DirectoryManagement (GUI)

Klasörleri ve içindekileri tarayarak kapasitelerine göre sıralama ve filtreleme işlemleri yapılabilir. Yeniden adlandırma veya silme işlemleri yapılabilir. Yazılım projeleri için bin/obj klasörlerini tüm klasörlerde tarayarak tespit edebilir ve silebilirsiniz.

`directory-size.bat` ile aynı işlevlerin WPF arayüzlü, taşınabilir Windows sürümü.

## Özellikler

- Başlangıçta disk veya klasör seçimi, sonra doğrudan alt öğelerin boyut tablosu (azalan sıra)
- OneDrive / SharePoint / Google Drive hariç tutma
- Seçilen klasörlerde `bin` ve `obj` silme (onay penceresi)
- Silme sonrası otomatik yeniden tarama
- Günlük alanı ve **Temizle** / **Yeniden Tara**
- Modern arayüz, **açık / koyu tema** (tercih kaydedilir)

## Portable exe oluşturma

```bat
publish-portable.bat
```

Otomatik: Cursor agent işi bittiğinde (proje `stop` hook) `DirectoryManagement` altında değişiklik varsa `publish-portable.bat /nopause` çalışır. Çıktı: `publish\win-x64\DirectoryManagement-x64.exe` ve `publish\win-x86\DirectoryManagement-x86.exe`

Manuel (pencere beklemeden): `publish-portable.bat /nopause`

`DirectoryManagement.exe` çalıştırıldığında tarama kökünü seçersiniz; gezinme ve `bin/obj` işlemleri **seçilen köke** göre yapılır.

## Gereksinim (yalnızca derleme için)

- .NET 10 SDK

Çalıştırmak için SDK gerekmez; self-contained exe yeterlidir.
