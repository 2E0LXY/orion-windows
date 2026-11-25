using CommunityToolkit.Mvvm.ComponentModel;

namespace Orion
{
    public partial class PasswordEntry : ObservableObject
    {
        [ObservableProperty]
        public partial long Id { get; set; }

        [ObservableProperty]
        public partial long ProfileId { get; set; }

        [ObservableProperty]
        public partial string Url { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Username { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Password { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string VerificationKey { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string VerificationCode { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PasswordDisplay))]
        [NotifyPropertyChangedFor(nameof(RevealGlyph))]
        public partial bool IsPasswordRevealed { get; set; }

        public bool HasPassword => !string.IsNullOrEmpty(Password);

        public string PasswordDisplay =>
            !HasPassword ? "" :
            IsPasswordRevealed ? Password : "••••••••";

        public string RevealGlyph => IsPasswordRevealed ? "" : "";
    }
}
