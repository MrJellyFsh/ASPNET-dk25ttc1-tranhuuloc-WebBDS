# ASPNET-dk25ttc1-tranhuuloc-WebBDS


NhaTot is a Vietnamese real estate platform designed to serve three primary user roles: property seekers, real estate agents who create and manage property listings, and administrators responsible for moderating listings before they are published..


Planned Features:
 - Homepage showcasing featured properties with quick search by location and property category .
 - Property search page with advanced filtering by keyword, sale/rental transaction type, location, property type, price range, floor area, and number of bedrooms, including pagination and VIP listing prioritization.
 - Property detail page displaying photo galleries, asking price, floor area, address, contact information, and an approximate location map
 - User registration and authentication using email/password with role-based access control (Customer, Agent, Administrator)
 - Agent onboarding workflow allowing users to apply for real estate agent status
 - Agent dashboard for creating, editing, deleting, and managing property listings through the Draft, Pending Review, Rejected, and Published lifecycle.
 - Administrative moderation workflow supporting listing approval/rejection, moderation notes, and a complete audit trail of review history.
 - Listing subscription plans (Free, Standard, VIP), an internal wallet system, and a payment architecture prepared for VNPay and MoMo integration. The current payment webhook implementation includes only a demonstration adapter,production payment gateways require merchant credentials and configuration.
 - Health check API endpoints supporting liveness and readiness probes for production deployments.
 - End-to-end (E2E) Playwright test coverage for the public property catalogue workflow.
 - OpenStreetMap is used as the default map provider, with optional support for Google Maps Embed
 - Publicly displayed property coordinates are intentionally rounded to approximately 1km to protect the precise location of listed properties and preserve seller privacy. **
      
Tech use:
 - Typescript
 - React
 - Tailwind CSS
 - NextJS
 - BcryptJS (pass sys)
 - AuthJS JWT
 - Prisma
Storage:
 - SQL + Cloudinary

