using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Logic_examples;
using LanguageExt;

namespace SmallShopBigAmbitions.Pages
{
    public class IndexModel : PageModel
    {
        private readonly TraceableIOLoggerExample _loggerExample;

        public IndexModel(TraceableIOLoggerExample loggerExample)
        {
            _loggerExample = loggerExample;
        }

        public string ResultMessage { get; private set; }

        public async Task<IActionResult> OnPostRunExampleAsync()
        {
            var result = await _loggerExample.Example();

            ResultMessage = result.Match(
                Succ: profile => $"Successfully enriched user profile: {profile.User}, {profile.Profile}, {profile.Badge}, {profile.Extra}",
                Fail: error => $"Failed to enrich user profile: {error.Message}"
            );

            return Page();
        }
    }
}
