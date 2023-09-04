namespace CDUniTap.Interfaces;

public interface ICasAuthenticatedApi
{
    public Task<bool> AuthenticateByCas();
}