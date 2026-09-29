/**
 * Turkish is the source dictionary: its keys define the Dictionary type, so a key missing from
 * another language is a compile error. Placeholders use {name}.
 *
 * Error keys mirror the API's stable codes (ADR-0006): `error.<code>` and `validation.<rule>`.
 */
export const tr = {
  "app.name": "Akiron CRM",
  "app.tagline": "Müşteriden işe, işten tahsilata.",

  "common.loading": "Yükleniyor…",
  "common.soon": "Yakında",
  "common.retry": "Tekrar dene",

  "auth.login.title": "Giriş yap",
  "auth.login.description": "Hesabınıza e-posta ve parolanızla girin.",
  "auth.login.submit": "Giriş yap",
  "auth.login.noAccount": "Hesabınız yok mu?",
  "auth.login.toRegister": "Kayıt olun",
  "auth.register.title": "Hesap oluştur",
  "auth.register.description": "Ajansınızı oluşturun; ilk kullanıcı olarak sahibi siz olursunuz.",
  "auth.register.submit": "Hesap oluştur",
  "auth.register.hasAccount": "Zaten hesabınız var mı?",
  "auth.register.toLogin": "Giriş yapın",
  "auth.field.organizationName": "Ajans / firma adı",
  "auth.field.fullName": "Ad soyad",
  "auth.field.email": "E-posta",
  "auth.field.password": "Parola",
  "auth.field.passwordHint": "En az {minLength} karakter.",
  "auth.logout": "Çıkış yap",
  "auth.sessionExpired": "Oturumunuz sona erdi, lütfen tekrar giriş yapın.",

  "nav.section.work": "Çalışma alanı",
  "nav.section.settings": "Ayarlar",
  "nav.dashboard": "Panel",
  "nav.customers": "Müşteriler",
  "nav.jobs": "İş emirleri",
  "nav.quotes": "Teklifler",
  "nav.content": "İçerik takvimi",
  "nav.finance": "Tahsilat",
  "nav.team": "Ekip",

  "dashboard.welcome": "Hoş geldin, {name}",
  "dashboard.intro": "{tenant} çalışma alanı hazır. Modüller geldikçe burası dolacak.",
  "dashboard.next.title": "Sırada ne var?",
  "dashboard.next.customers": "Müşteri kartları ve zaman çizelgesi",
  "dashboard.next.jobs": "İş emirleri, görevler ve zaman takibi",
  "dashboard.next.quotes": "Teklif, sözleşme ve PayTR ödeme linki",

  "team.title": "Ekip",
  "team.description": "Bu çalışma alanındaki kişiler.",
  "team.column.name": "Ad soyad",
  "team.column.email": "E-posta",
  "team.column.role": "Rol",
  "team.column.joinedAt": "Katılma",
  "team.empty": "Henüz kimse yok.",
  "team.noPermission": "Ekip listesini görme yetkiniz yok.",

  "role.owner": "Sahip",
  "role.admin": "Yönetici",
  "role.member": "Üye",

  "user.menu.theme": "Tema",
  "user.menu.theme.light": "Açık",
  "user.menu.theme.dark": "Koyu",
  "user.menu.theme.system": "Sistem",
  "user.menu.language": "Dil",

  "error.common.validation.failed": "Bazı alanları kontrol edin.",
  "error.common.request.malformed": "İstek okunamadı.",
  "error.common.auth.unauthenticated": "Oturum açmanız gerekiyor.",
  "error.common.auth.forbidden": "Bu işlem için yetkiniz yok.",
  "error.common.conflict": "Kayıt başka bir kayıtla çakışıyor.",
  "error.common.rate_limited": "Çok fazla deneme yapıldı, biraz bekleyin.",
  "error.common.unexpected": "Beklenmeyen bir hata oluştu. Sorun sürerse bu kodu iletin: {correlationId}",
  "error.common.network": "Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.",
  "error.identity.user.email_taken": "Bu e-posta ile kayıtlı bir hesap zaten var.",
  "error.identity.auth.invalid_credentials": "E-posta veya parola hatalı.",
  "error.identity.auth.session_expired": "Oturumunuz sona erdi, lütfen tekrar giriş yapın.",

  "validation.required": "Bu alan zorunlu.",
  "validation.email": "Geçerli bir e-posta adresi girin.",
  "validation.min_length": "En az {minLength} karakter olmalı.",
  "validation.max_length": "En fazla {maxLength} karakter olabilir.",
  "validation.length": "{minLength}–{maxLength} karakter arasında olmalı.",
  "validation.between": "{from} ile {to} arasında olmalı.",
  "validation.format": "Biçim geçersiz.",
  "validation.invalid_value": "Değer geçersiz.",
} as const;

export type TranslationKey = keyof typeof tr;
export type Dictionary = Record<TranslationKey, string>;
