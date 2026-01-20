# Run this script to commit the deployment guide to your repository

git add DEPLOYMENT.md
git add src/BillsApi/web.config
git add src/BillsApi/Program.cs
git commit -m "Add comprehensive deployment guide and fix production configuration

- Add DEPLOYMENT.md with complete IIS and SQL Server deployment instructions
- Update web.config to include Production environment variable
- Disable automatic migrations (tables created manually)
- Include troubleshooting guide for common deployment issues"
git push origin main

Write-Host "Deployment guide has been committed and pushed to GitHub!" -ForegroundColor Green
