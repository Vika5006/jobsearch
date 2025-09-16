# Why
To automate checking for new positions

# How 
Greenhouse offers public [Job board API] (https://developers.greenhouse.io/job-board.html#introduction), so with a little work it could be used for polling and alerting oneself on the new postings, when publicly available alerts from individual employers aren't quick enough. 
- Poll the boards for selected companies (tried to select remote friendly) every 10 minutes (can change)
- Check if a job satisfies the criteria (in US, updated in the last hour, not sent yet), send me an email.
  - Add to cache of sent jobs if already found
 
# Gotcha
It's not clear what timezone is updated_at in. The closest answer could be to parse Location, however Location doesn't have a predefined format across companies, so instead compare with UTCNow - 60 mins for each US timezone and send if any satisfies. Hence the cache, to avoid getting duplicate emails.
