/** Permission keys as issued by the API (Swan.Domain.Identity.PermissionKeys). Checked on the server; the UI only hides controls. */
export const PermissionKeys = {
  ArticleWrite: 'article.write',
  ArticleSubmit: 'article.submit',
  ArticleReview: 'article.review',
  ArticlePublish: 'article.publish',
  ArticleImportUnverified: 'article.import_unverified',
  PublishingDraft: 'publishing.draft',
  SiteRead: 'site.read',
  SiteManage: 'site.manage',
  UserManage: 'user.manage',
  AuditRead: 'audit.read',
} as const;
