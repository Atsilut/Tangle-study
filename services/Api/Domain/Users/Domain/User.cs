namespace Api.Domain.Users.Domain
{
    public class User
    {
        public Guid Id { get; private set; }
        public string Email { get; private set; }
        public string Password { get; private set; }
        public string Nickname { get; private set; }

        private User() { }

        public User(Guid id, string email, string password, string nickname)
        {
            Id = id;
            Email = email;
            Password = password;
            Nickname = nickname;
        }
    }
}
