"""Config flow for Desktop Companion."""

from __future__ import annotations

import voluptuous as vol

from homeassistant import config_entries
from homeassistant.const import CONF_NAME

from .const import DOMAIN


class DesktopCompanionConfigFlow(config_entries.ConfigFlow, domain=DOMAIN):
    """Handle one integration entry which owns paired Windows devices."""

    VERSION = 1

    async def async_step_user(self, user_input=None):
        """Create the integration hub entry."""
        await self.async_set_unique_id(DOMAIN)
        self._abort_if_unique_id_configured()

        if user_input is not None:
            name = " ".join(user_input[CONF_NAME].split())
            return self.async_create_entry(
                title=name,
                data={CONF_NAME: name, "devices": {}},
            )

        return self.async_show_form(
            step_id="user",
            data_schema=vol.Schema(
                {vol.Required(CONF_NAME, default="Desktop Companion"): str}
            ),
        )
